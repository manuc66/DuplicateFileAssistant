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

    /// <summary>
    /// Returns true when <paramref name="path"/> is <paramref name="folder"/> or one of its
    /// descendants. Trailing separators and filesystem roots are handled, and a sibling
    /// whose name only starts with the folder name is never considered a child.
    /// </summary>
    public static bool IsUnder(string path, string folder)
    {
        string fullPath = Path.GetFullPath(path);
        string fullFolder = Path.GetFullPath(folder);
        string trimmedFolder = fullFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (fullPath.Equals(trimmedFolder, PathComparison))
        {
            return true;
        }

        // Trimming a filesystem root ("/" or "C:\") leaves an empty string or a drive
        // specifier; the separator already belongs to the root in that case.
        string prefix = trimmedFolder.Length == 0 || trimmedFolder.EndsWith(Path.VolumeSeparatorChar)
            ? fullFolder
            : trimmedFolder + Path.DirectorySeparatorChar;

        if (!prefix.EndsWith(Path.DirectorySeparatorChar))
        {
            prefix += Path.DirectorySeparatorChar;
        }

        return fullPath.StartsWith(prefix, PathComparison);
    }

    public static string ExpandPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Path.GetFullPath(ExpandUserProfile(path.Trim()));
    }

    /// <summary>
    /// Non-throwing variant of <see cref="ExpandPath"/>. Invalid or unsupported paths are
    /// reported instead of escaping into the caller (a broken setting or exclusion must
    /// not make a scan fail).
    /// </summary>
    public static bool TryExpandPath(string? path, out string expanded)
    {
        expanded = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            expanded = ExpandPath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            return false;
        }
    }

    private static string ExpandUserProfile(string path)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (path == "~")
        {
            return userProfile;
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) ||
            path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(userProfile, path[2..]);
        }

        return Environment.ExpandEnvironmentVariables(path);
    }
}
