namespace FileCompare;

public sealed record DuplicateFile(string Path, long Length, DateTime LastWriteTimeUtc)
{
    public string SizeText => FileSizeFormatter.Format(Length);

    public static IEqualityComparer<DuplicateFile> Comparer { get; } = new DuplicateFileComparer();

    public static bool TryCreate(string path, out DuplicateFile? file)
    {
        try
        {
            FileInfo fileInfo = new(path);

            if (!fileInfo.Exists)
            {
                file = null;
                return false;
            }

            file = new DuplicateFile(path, fileInfo.Length, fileInfo.LastWriteTimeUtc);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            file = null;
            return false;
        }
    }

    private sealed class DuplicateFileComparer : IEqualityComparer<DuplicateFile>
    {
        public bool Equals(DuplicateFile? x, DuplicateFile? y)
        {
            return x is not null && y is not null && PathUtilities.PathComparer.Equals(x.Path, y.Path);
        }

        public int GetHashCode(DuplicateFile obj)
        {
            return PathUtilities.PathComparer.GetHashCode(obj.Path);
        }
    }
}
