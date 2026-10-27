namespace FileCompare;

public static class EmptyDirectoryService
{
    public static IReadOnlyList<string> FindEmptyDirectories(string root, CancellationToken cancellationToken,
        Func<string, bool>? directoryFilter = null)
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

            if (directoryFilter != null && !directoryFilter(directory))
            {
                continue;
            }

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

    public static int DeleteEmptyDirectories(IReadOnlyList<string> directories, CancellationToken cancellationToken,
        Action<string, bool>? result = null)
    {
        return DeleteEmptyDirectoriesAsync(directories, cancellationToken, result)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    public static async Task<int> DeleteEmptyDirectoriesAsync(IReadOnlyList<string> directories,
        CancellationToken cancellationToken, Action<string, bool>? result = null, PauseToken? pauseToken = null,
        Action<int, int, string?>? progress = null)
    {
        int deleted = 0;
        int processed = 0;

        foreach (string directory in directories)
        {
            if (pauseToken != null)
            {
                await pauseToken.WaitWhilePausedAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke(processed, directories.Count, directory);
            processed++;

            try
            {
                Directory.Delete(directory, recursive: false);
                deleted++;
                result?.Invoke(directory, true);
            }
            catch (IOException)
            {
                result?.Invoke(directory, false);
            }
            catch (UnauthorizedAccessException)
            {
                result?.Invoke(directory, false);
            }
        }

        progress?.Invoke(processed, directories.Count, null);
        return deleted;
    }

    private static int Depth(string path)
    {
        return path.Count(character =>
            character == Path.DirectorySeparatorChar ||
            character == Path.AltDirectorySeparatorChar);
    }
}
