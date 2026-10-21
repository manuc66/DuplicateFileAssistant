using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileCompare;

/// <summary>
/// Loads and persists the user's trash choice. Defaults come from the application
/// configuration; once the user saves the settings they are stored in the user profile.
/// </summary>
public sealed class TrashSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _sync = new();
    private readonly string _settingsFile;
    private TrashSettings _current;

    public TrashSettingsStore(string settingsFile, TrashSettings defaults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFile);
        ArgumentNullException.ThrowIfNull(defaults);

        _settingsFile = PathUtilities.ExpandPath(settingsFile);
        _current = Load(Normalize(defaults, defaults));
    }

    public event EventHandler? Changed;

    public TrashSettings Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public static TrashMode ParseMode(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out TrashMode mode) &&
               Enum.IsDefined(mode)
            ? mode
            : TrashMode.Application;
    }

    public static string NormalizeFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        string expanded = PathUtilities.ExpandPath(folder);
        string root = Path.GetPathRoot(expanded) ?? string.Empty;

        if (expanded.Equals(root, PathUtilities.PathComparison))
        {
            throw new ArgumentException("The trash folder cannot be a filesystem root.", nameof(folder));
        }

        if (File.Exists(expanded))
        {
            throw new ArgumentException("The trash folder cannot be a file.", nameof(folder));
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile) &&
            expanded.Equals(PathUtilities.ExpandPath(userProfile), PathUtilities.PathComparison))
        {
            throw new ArgumentException("The trash folder cannot be the user profile folder.", nameof(folder));
        }

        return expanded;
    }

    public void Save(TrashSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        TrashSettings normalized = Normalize(settings, Current);
        string json = JsonSerializer.Serialize(normalized, JsonOptions);
        AtomicFile.WriteAllText(_settingsFile, json);

        lock (_sync)
        {
            _current = normalized;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private TrashSettings Load(TrashSettings defaults)
    {
        if (!File.Exists(_settingsFile))
        {
            return Normalize(defaults, defaults);
        }

        try
        {
            string json = File.ReadAllText(_settingsFile);
            TrashSettings? loaded = JsonSerializer.Deserialize<TrashSettings>(json, JsonOptions);
            return loaded is null ? Normalize(defaults, defaults) : Normalize(loaded, defaults);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A broken user setting must never prevent the application from starting.
            return Normalize(defaults, defaults);
        }
    }

    private static TrashSettings Normalize(TrashSettings settings, TrashSettings fallback)
    {
        TrashMode mode = Enum.IsDefined(settings.Mode) ? settings.Mode : fallback.Mode;

        string folder;
        try
        {
            folder = NormalizeFolder(string.IsNullOrWhiteSpace(settings.TrashFolder)
                ? fallback.TrashFolder
                : settings.TrashFolder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            folder = NormalizeFolder(fallback.TrashFolder);
        }

        return new TrashSettings(mode, folder);
    }
}
