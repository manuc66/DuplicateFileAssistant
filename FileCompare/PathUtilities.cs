namespace FileCompare;

public static class PathUtilities
{
    public static StringComparison PathComparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static StringComparer PathComparer { get; } =
        PathComparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public static bool IsUnder(string path, string folder)
    {
        string fullPath = Path.GetFullPath(path);
        string fullFolder = Path.GetFullPath(folder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return fullPath.Equals(fullFolder, PathComparison) ||
               fullPath.StartsWith(fullFolder + Path.DirectorySeparatorChar, PathComparison) ||
               fullPath.StartsWith(fullFolder + Path.AltDirectorySeparatorChar, PathComparison);
    }

    public static string ExpandPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string expanded = Environment.ExpandEnvironmentVariables(path.Trim());

        if (expanded == "~")
        {
            expanded = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else if (expanded.StartsWith("~/", StringComparison.Ordinal) ||
                 expanded.StartsWith("~\\", StringComparison.Ordinal))
        {
            expanded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                expanded[2..]);
        }

        return Path.GetFullPath(expanded);
    }
}
