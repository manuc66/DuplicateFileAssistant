using System.Text.Json;

namespace FileCompare;

/// <summary>
/// Loads and persists the user's model folder choice. Missing or corrupt settings
/// fall back to the application default without preventing startup.
/// </summary>
public sealed class ModelSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _settingsFile;
    private ModelSettings _current;

    public ModelSettingsStore(string settingsFile, ModelSettings defaults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFile);
        ArgumentNullException.ThrowIfNull(defaults);

        _settingsFile = PathUtilities.ExpandPath(settingsFile);
        _current = Load(Normalize(defaults, defaults));
    }

    public event EventHandler? Changed;

    public ModelSettings Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public static string NormalizeFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        string expanded = PathUtilities.ExpandPath(folder);
        string root = Path.GetPathRoot(expanded) ?? string.Empty;

        if (expanded.Equals(root, PathUtilities.PathComparison))
        {
            throw new ArgumentException("The models folder cannot be a filesystem root.", nameof(folder));
        }

        if (File.Exists(expanded))
        {
            throw new ArgumentException("The models folder cannot be a file.", nameof(folder));
        }

        return expanded;
    }

    public void Save(ModelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ModelSettings normalized = Normalize(settings, Current);
        string json = JsonSerializer.Serialize(normalized, JsonOptions);
        AtomicFile.WriteAllText(_settingsFile, json);

        lock (_sync)
        {
            _current = normalized;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private ModelSettings Load(ModelSettings defaults)
    {
        if (!File.Exists(_settingsFile))
        {
            return Normalize(defaults, defaults);
        }

        try
        {
            string json = File.ReadAllText(_settingsFile);
            ModelSettings? loaded = JsonSerializer.Deserialize<ModelSettings>(json, JsonOptions);
            return loaded is null ? Normalize(defaults, defaults) : Normalize(loaded, defaults);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or JsonException)
        {
            return Normalize(defaults, defaults);
        }
    }

    private static ModelSettings Normalize(ModelSettings settings, ModelSettings fallback)
    {
        string folder;

        try
        {
            folder = NormalizeFolder(string.IsNullOrWhiteSpace(settings.ModelsFolder)
                ? fallback.ModelsFolder
                : settings.ModelsFolder);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            folder = NormalizeFolder(fallback.ModelsFolder);
        }

        return new ModelSettings(folder);
    }
}
