namespace FileCompare;

public sealed record MergeResult(int Moved, int TrashedDuplicates, int Conflicts, int Failed);

public static class MergeExecutor
{
    public static async Task<MergeResult> ExecuteAsync(IReadOnlyList<MergePlanItem> plan, Trash trash,
        CancellationToken cancellationToken, OperationJournal? journal = null)
    {
        int moved = 0;
        int trashed = 0;
        int conflicts = 0;
        int failed = 0;

        foreach (MergePlanItem item in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

                        await MoveAsync(item.SourcePath, item.DestinationPath, new FileInfo(item.SourcePath),
                            cancellationToken);
                        moved++;
                        journal?.Write("move", item.SourcePath, item.DestinationPath, "ok", null);
                        break;

                    case MergeAction.TrashDuplicate:
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

        return new MergeResult(moved, trashed, conflicts, failed);
    }

    private static async Task MoveAsync(string sourcePath, string destinationPath, FileInfo sourceInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            File.Move(sourcePath, destinationPath);
        }
        catch (IOException) when (File.Exists(sourcePath) && !File.Exists(destinationPath))
        {
            await using (FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             81920, FileOptions.Asynchronous))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            FileMetadata.Copy(sourceInfo, destinationPath);
            File.Delete(sourcePath);
        }
    }
}
