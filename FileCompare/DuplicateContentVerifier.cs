namespace FileCompare;

public static class DuplicateContentVerifier
{
    public static List<HashSet<DuplicateFile>> Partition(IEnumerable<DuplicateFile> files,
        CancellationToken cancellationToken)
    {
        List<(DuplicateFile Representative, List<DuplicateFile> Files)> groups = new();

        foreach (DuplicateFile file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool added = false;

            foreach ((DuplicateFile representative, List<DuplicateFile> groupFiles) in groups)
            {
                if (FilesAreEqual(representative.Path, file.Path))
                {
                    groupFiles.Add(file);
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                groups.Add((file, new List<DuplicateFile>(1) { file }));
            }
        }

        return groups
            .Where(group => group.Files.Count > 1)
            .Select(group => new HashSet<DuplicateFile>(group.Files, DuplicateFile.Comparer))
            .ToList();
    }

    private static bool FilesAreEqual(string first, string second)
    {
        try
        {
            return FastFileCompare.AreFilesEqual(new FileInfo(first), new FileInfo(second));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
