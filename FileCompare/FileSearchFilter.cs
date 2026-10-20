namespace FileCompare;

public sealed class FileSearchFilter
{
    private readonly HashSet<string> _extensions;
    private readonly string[] _excludedFolders;

    private FileSearchFilter(long minimumSize, HashSet<string> extensions, string[] excludedFolders)
    {
        MinimumSize = Math.Max(0, minimumSize);
        _extensions = extensions;
        _excludedFolders = excludedFolders;
    }

    public long MinimumSize { get; }

    public static FileSearchFilter FromText(long minimumSize, string? extensions, string? excludedFolders)
    {
        HashSet<string> parsedExtensions = new(StringComparer.OrdinalIgnoreCase);

        foreach (string extension in Split(extensions))
        {
            parsedExtensions.Add(extension.StartsWith('.') ? extension : $".{extension}");
        }

        string[] parsedExcludedFolders = Split(excludedFolders)
            .Select(PathUtilities.ExpandPath)
            .ToArray();

        return new FileSearchFilter(minimumSize, parsedExtensions, parsedExcludedFolders);
    }

    public bool MatchesFile(IFileInfo fileInfo)
    {
        return !fileInfo.IsLink() &&
               !fileInfo.IsFifoFile() &&
               fileInfo.Length >= MinimumSize;
    }

    public bool MatchesPath(string path)
    {
        return MatchesDirectory(path) &&
               (_extensions.Count == 0 || _extensions.Contains(Path.GetExtension(path)));
    }

    public bool MatchesDirectory(string path)
    {
        if (_excludedFolders.Length == 0)
        {
            return true;
        }

        string fullPath = Path.GetFullPath(path);

        foreach (string excludedFolder in _excludedFolders)
        {
            if (fullPath.Equals(excludedFolder, PathUtilities.PathComparison) ||
                fullPath.StartsWith(excludedFolder + Path.DirectorySeparatorChar, PathUtilities.PathComparison) ||
                fullPath.StartsWith(excludedFolder + Path.AltDirectorySeparatorChar, PathUtilities.PathComparison))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> Split(string? value)
    {
        return (value ?? string.Empty).Split(
            new[] { ',', ';' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
