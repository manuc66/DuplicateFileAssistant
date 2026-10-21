namespace FileCompare;

public static class CrossFolderDuplicateFinder
{
    public static IReadOnlyList<CrossFolderDuplicate> Find(string sourcePath, string otherPath,
        Func<IFileInfo, bool> filter, Func<string, byte[]> hash, TextWriter textDisplayProgress,
        Action<int> updateProgress, CancellationToken cancellationToken, Func<string, bool>? pathFilter = null)
    {
        Dictionary<HashKey, (HashSet<string> inA, HashSet<string> inB)> groups =
            DupProvider.FindDuplicateByHash(
                sourcePath,
                SearchOption.AllDirectories,
                otherPath,
                SearchOption.AllDirectories,
                filter,
                textDisplayProgress,
                hash,
                cancellationToken,
                pathFilter);

        List<CrossFolderDuplicate> results = new();

        foreach ((_, (HashSet<string> sources, HashSet<string> others)) in groups)
        {
            string? other = others.FirstOrDefault();

            if (other is null || !DuplicateFile.TryCreate(other, out DuplicateFile? otherFile) || otherFile is null)
            {
                continue;
            }

            foreach (string source in sources)
            {
                if (DuplicateFile.TryCreate(source, out DuplicateFile? sourceFile) && sourceFile is not null)
                {
                    results.Add(new CrossFolderDuplicate(sourceFile, otherFile));
                }
            }
        }

        return results
            .OrderBy(result => result.Source.Path, PathUtilities.PathComparer)
            .ToList();
    }
}
