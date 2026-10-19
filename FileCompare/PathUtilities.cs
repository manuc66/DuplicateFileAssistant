namespace FileCompare;

public static class PathUtilities
{
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
