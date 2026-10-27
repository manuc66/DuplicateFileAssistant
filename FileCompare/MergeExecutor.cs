namespace FileCompare;

public sealed record MergeResult(int Moved, int TrashedDuplicates, int Conflicts, int Failed);

public static class MergeExecutor
{
    public static async Task<MergeResult> ExecuteAsync(IReadOnlyList<MergePlanItem> plan, Trash trash,
        CancellationToken cancellationToken, OperationJournal? journal = null, PauseToken? pauseToken = null,
        Action<int, int, string?>? progress = null)
    {
        int moved = 0;
        int trashed = 0;
        int conflicts = 0;
        int failed = 0;
        int processed = 0;

        foreach (MergePlanItem item in plan)
        {
            if (pauseToken != null)
            {
                await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke(processed, plan.Count, item.SourcePath);
            processed++;

            try
            {
                switch (item.Action)
                {
                    case MergeAction.Move:
                        string? directory = Path.GetDirectoryName(item.DestinationPath);
                        if (directory != null)
                        {
                            Directory.CreateDirectory(directory);
                        }

                        await TrashFileOperations.MoveAsync(item.SourcePath, item.DestinationPath,
                            new FileInfo(item.SourcePath), cancellationToken);
                        moved++;
                        journal?.Write("move", item.SourcePath, item.DestinationPath, "ok", null);
                        break;

                    case MergeAction.TrashDuplicate:
                        // The destination may have disappeared or changed since the plan
                        // was built. Never trash the source unless a verified duplicate is
                        // still present.
                        if (!File.Exists(item.DestinationPath) ||
                            !FastFileCompare.AreFilesEqual(
                                new FileInfo(item.SourcePath),
                                new FileInfo(item.DestinationPath)))
                        {
                            throw new IOException(
                                $"The duplicate destination changed since the plan was created: {item.DestinationPath}");
                        }

                        await trash.DeleteAsync(item.SourcePath, item.Source.Length, item.Source.LastWriteTimeUtc,
                            cancellationToken);
                        trashed++;
                        journal?.Write("trash-duplicate", item.SourcePath, item.DestinationPath, "ok", null);
                        break;

                    case MergeAction.Conflict:
                        conflicts++;
                        journal?.Write("conflict", item.SourcePath, item.DestinationPath, "skipped", null);
                        break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                journal?.Write(item.Action.ToString(), item.SourcePath, item.DestinationPath, "failed", ex.Message);
            }
        }

        progress?.Invoke(processed, plan.Count, null);
        return new MergeResult(moved, trashed, conflicts, failed);
    }
}
