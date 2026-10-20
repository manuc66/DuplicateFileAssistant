namespace FileCompare;

public static class EmptyDirectoryService
{
    public static IReadOnlyList<string> FindEmptyDirectories(string root, CancellationToken cancellationToken)
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

        List<string> directories = Directory.EnumerateDirectories(root, "*", options)
            .OrderByDescending(Depth)
            .ThenBy(path => path, PathUtilities.PathComparer)
            .ToList();

        HashSet<string> removable = new(PathUtilities.PathComparer);

        foreach (string directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool isEmpty = true;

            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (Directory.Exists(entry) && removable.Contains(entry))
                    {
                        continue;
                    }

                    isEmpty = false;
                    break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                isEmpty = false;
            }

            if (isEmpty)
            {
                removable.Add(directory);
            }
        }

        return directories.Where(removable.Contains).ToList();
    }

    public static int DeleteEmptyDirectories(IReadOnlyList<string> directories, CancellationToken cancellationToken)
    {
        int deleted = 0;

        foreach (string directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Directory.Delete(directory, recursive: false);
                deleted++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return deleted;
    }

    private static int Depth(string path)
    {
        return path.Count(character =>
            character == Path.DirectorySeparatorChar ||
            character == Path.AltDirectorySeparatorChar);
    }
}
