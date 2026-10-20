namespace FileCompare;

public enum MergeAction
{
    Move,
    TrashDuplicate,
    Conflict
}

public sealed record MergePlanItem(
    MergeAction Action,
    string SourcePath,
    string DestinationPath,
    DuplicateFile Source,
    DuplicateFile? Destination);

public static class MergePlanner
{
    public static IReadOnlyList<MergePlanItem> CreatePlan(string sourceRoot, string destinationRoot,
        CancellationToken cancellationToken, Func<string, bool>? sourcePathFilter = null)
    {
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Source folder not found: {sourceRoot}");
        }

        if (PathUtilities.IsUnder(destinationRoot, sourceRoot) || PathUtilities.IsUnder(sourceRoot, destinationRoot))
        {
            throw new InvalidOperationException("Source and destination folders must not contain each other.");
        }

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchType = OperatingSystem.IsWindows() ? MatchType.Win32 : MatchType.Simple,
            ReturnSpecialDirectories = false
        };

        List<MergePlanItem> plan = new();

        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sourcePathFilter != null && !sourcePathFilter(sourcePath))
            {
                continue;
            }

            if (!DuplicateFile.TryCreate(sourcePath, out DuplicateFile? source) || source is null)
            {
                continue;
            }

            string destinationPath = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, sourcePath));

            if (!File.Exists(destinationPath))
            {
                plan.Add(new MergePlanItem(MergeAction.Move, sourcePath, destinationPath, source, null));
                continue;
            }

            if (!DuplicateFile.TryCreate(destinationPath, out DuplicateFile? destination) || destination is null)
            {
                plan.Add(new MergePlanItem(MergeAction.Conflict, sourcePath, destinationPath, source, null));
                continue;
            }

            MergeAction action = FastFileCompare.AreFilesEqual(new FileInfo(sourcePath), new FileInfo(destinationPath))
                ? MergeAction.TrashDuplicate
                : MergeAction.Conflict;

            plan.Add(new MergePlanItem(action, sourcePath, destinationPath, source, destination));
        }

        return plan.OrderBy(item => item.SourcePath, PathUtilities.PathComparer).ToList();
    }
}
