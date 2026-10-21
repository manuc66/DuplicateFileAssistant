namespace FileCompare;

public static class InvalidSymbolicLinkFinder
{
    public static IReadOnlyList<InvalidSymbolicLink> Find(string root, FileSearchFilter filter,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory not found: {root}");
        }

        List<InvalidSymbolicLink> results = new();
        Stack<string> pendingDirectories = new();
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pendingDirectories.Pop();

            string[] entries;

            try
            {
                entries = Directory.GetFileSystemEntries(directory, "*", new EnumerationOptions
                {
                    RecurseSubdirectories = false,
                    IgnoreInaccessible = true,
                    AttributesToSkip = 0,
                    MatchType = OperatingSystem.IsWindows() ? MatchType.Win32 : MatchType.Simple,
                    ReturnSpecialDirectories = false
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileInfo fileInfo = new(entry);
                string? linkTarget;

                try
                {
                    linkTarget = fileInfo.LinkTarget;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (linkTarget != null)
                {
                    bool targetExists;

                    try
                    {
                        FileSystemInfo? target = fileInfo.ResolveLinkTarget(returnFinalTarget: true);
                        targetExists = target != null &&
                                       (File.Exists(target.FullName) || Directory.Exists(target.FullName));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                    {
                        targetExists = false;
                    }

                    if (!targetExists && filter.MatchesPath(entry))
                    {
                        results.Add(new InvalidSymbolicLink(entry, linkTarget));
                    }

                    continue;
                }

                if (Directory.Exists(entry) && filter.MatchesDirectory(entry))
                {
                    pendingDirectories.Push(entry);
                }
            }
        }

        return results
            .OrderBy(link => link.Path, PathUtilities.PathComparer)
            .ToList();
    }
}
