namespace FileCompare;

public static class BigFileFinder
{
    public static IReadOnlyList<DuplicateFile> Find(string root, int maximumFiles, FileSearchFilter filter,
        CancellationToken cancellationToken)
    {
        return Find(root, maximumFiles, filter, long.MaxValue, cancellationToken);
    }

    public static IReadOnlyList<DuplicateFile> Find(string root, int maximumFiles, FileSearchFilter filter,
        long maximumSize, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory not found: {root}");
        }

        long effectiveMaximum = maximumSize <= 0 ? long.MaxValue : maximumSize;

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchType = OperatingSystem.IsWindows() ? MatchType.Win32 : MatchType.Simple,
            ReturnSpecialDirectories = false
        };

        List<DuplicateFile> results = new();

        foreach (string path in Directory.EnumerateFiles(root, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!filter.MatchesPath(path) ||
                !DuplicateFile.TryCreate(path, out DuplicateFile? file) ||
                file is null ||
                file.Length < filter.MinimumSize ||
                file.Length > effectiveMaximum)
            {
                continue;
            }

            results.Add(file);
        }

        return results
            .OrderByDescending(file => file.Length)
            .ThenBy(file => file.Path, PathUtilities.PathComparer)
            .Take(Math.Max(0, maximumFiles))
            .ToList();
    }
}
