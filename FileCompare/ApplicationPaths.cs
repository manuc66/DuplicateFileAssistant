namespace FileCompare;

/// <summary>
/// Locations owned by the application that are not tied to a scanned folder.
/// </summary>
public static class ApplicationPaths
{
    public static string RootDirectory { get; } = ResolveRootDirectory();

    public static string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public static string ModelSettingsFile => Path.Combine(RootDirectory, "model-settings.json");

    public static string LastFolderFile => Path.Combine(RootDirectory, "last-folder.json");

    public static string FilterSettingsFile => Path.Combine(RootDirectory, "filter-settings.json");

    public static string SystemTrashDirectory => Path.Combine(RootDirectory, "system-trash");

    private static string ResolveRootDirectory()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetTempPath();
        }

        return Path.Combine(root, "DuplicateFileAssistant");
    }
}
