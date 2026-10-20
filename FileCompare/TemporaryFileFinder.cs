namespace FileCompare;

public static class TemporaryFileFinder
{
    private static readonly string[] Patterns =
    {
        ".tmp", ".temp", ".swp", ".swo", ".bak", ".old", ".orig", ".part", ".crdownload",
        ".cache", ".log", "~"
    };

    private static readonly string[] ExactNames =
    {
        ".DS_Store", "Thumbs.db", "desktop.ini"
    };

    public static IReadOnlyList<DuplicateFile> Find(string root, FileSearchFilter filter,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory not found: {root}");
        }

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

            if (!filter.MatchesPath(path) || !IsTemporaryFile(path) ||
                !DuplicateFile.TryCreate(path, out DuplicateFile? file) || file is null)
            {
                continue;
            }

            results.Add(file);
        }

        return results
            .OrderBy(file => file.Path, PathUtilities.PathComparer)
            .ToList();
    }

    private static bool IsTemporaryFile(string path)
    {
        string fileName = Path.GetFileName(path);

        if (ExactNames.Any(name => fileName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return Patterns.Any(pattern => fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
