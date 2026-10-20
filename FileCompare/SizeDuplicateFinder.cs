namespace FileCompare;

public static class SizeDuplicateFinder
{
    public static Dictionary<HashKey, HashSet<DuplicateFile>> Find(string root, SearchOption searchOption,
        FileSearchFilter filter, TextWriter textDisplayProgress, Action<int> updateProgress,
        CancellationToken cancellationToken, Func<string, bool>? pathFilter = null)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory not found: {root}");
        }

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = searchOption == SearchOption.AllDirectories,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchType = OperatingSystem.IsWindows() ? MatchType.Win32 : MatchType.Simple,
            ReturnSpecialDirectories = false
        };

        Dictionary<long, List<DuplicateFile>> filesBySize = new();
        int scanned = 0;

        foreach (string path in Directory.EnumerateFiles(root, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pathFilter != null && !pathFilter(path))
            {
                continue;
            }

            if (!filter.MatchesPath(path) ||
                !DuplicateFile.TryCreate(path, out DuplicateFile? file) ||
                file is null)
            {
                continue;
            }

            if (filesBySize.TryGetValue(file.Length, out List<DuplicateFile>? files))
            {
                files.Add(file);
            }
            else
            {
                filesBySize[file.Length] = new List<DuplicateFile>(1) { file };
            }

            scanned++;

            if (scanned % 1000 == 0)
            {
                updateProgress(0);
            }
        }

        Dictionary<HashKey, HashSet<DuplicateFile>> result = new();

        foreach ((long size, List<DuplicateFile> files) in filesBySize)
        {
            if (files.Count > 1)
            {
                result[HashKey.FromOwned(BitConverter.GetBytes(size))] =
                    new HashSet<DuplicateFile>(files, DuplicateFile.Comparer);
            }
        }

        textDisplayProgress.WriteLine($"Total files to analyze: {scanned}");
        updateProgress(100);
        return result;
    }
}
