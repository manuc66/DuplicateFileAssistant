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
        bool matchAllExtensions = false;

        foreach (string rawExtension in Split(extensions))
        {
            string extension = rawExtension.Trim();

            if (extension.StartsWith("*.", StringComparison.Ordinal))
            {
                extension = extension[1..];
            }
            else if (extension == "*")
            {
                matchAllExtensions = true;
                continue;
            }
            else if (extension.StartsWith('*'))
            {
                extension = extension[1..];
            }

            if (extension.Length > 0)
            {
                parsedExtensions.Add(extension.StartsWith('.') ? extension : $".{extension}");
            }
        }

        if (matchAllExtensions)
        {
            parsedExtensions.Clear();
        }

        List<string> parsedExcludedFolders = new();

        foreach (string excludedFolder in Split(excludedFolders))
        {
            if (PathUtilities.TryExpandPath(excludedFolder, out string expanded))
            {
                parsedExcludedFolders.Add(expanded);
            }
        }

        return new FileSearchFilter(minimumSize, parsedExtensions, parsedExcludedFolders.ToArray());
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
        foreach (string excludedFolder in _excludedFolders)
        {
            if (PathUtilities.IsUnder(path, excludedFolder))
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
