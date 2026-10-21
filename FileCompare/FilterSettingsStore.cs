using System.Text.Json;

namespace FileCompare;

/// <summary>
/// Persists the filter state of each tab. Missing or corrupt files fall back to
/// the supplied defaults; writes are skipped when nothing changed.
/// </summary>
public sealed class FilterSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _settingsFile;
    private Dictionary<string, FilterSettings> _entries;

    public FilterSettingsStore(string settingsFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFile);

        _settingsFile = PathUtilities.ExpandPath(settingsFile);
        _entries = Load();
    }

    public event EventHandler? Changed;

    public FilterSettings Get(string key, FilterSettings defaults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(defaults);

        lock (_sync)
        {
            return _entries.TryGetValue(key, out FilterSettings? settings)
                ? Normalize(settings, defaults)
                : defaults;
        }
    }

    public void Save(string key, FilterSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(settings);

        FilterSettings normalized = Normalize(settings, FilterSettings.Default);

        lock (_sync)
        {
            if (_entries.TryGetValue(key, out FilterSettings? existing) && existing == normalized)
            {
                return;
            }

            _entries[key] = normalized;
        }

        string? directory = Path.GetDirectoryName(_settingsFile);

        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        lock (_sync)
        {
            string json = JsonSerializer.Serialize(_entries, JsonOptions);
            string temporaryPath = _settingsFile + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _settingsFile, overwrite: true);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private Dictionary<string, FilterSettings> Load()
    {
        if (!File.Exists(_settingsFile))
        {
            return new Dictionary<string, FilterSettings>(StringComparer.Ordinal);
        }

        try
        {
            string json = File.ReadAllText(_settingsFile);
            return JsonSerializer.Deserialize<Dictionary<string, FilterSettings>>(json, JsonOptions)
                   ?? new Dictionary<string, FilterSettings>(StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or JsonException)
        {
            return new Dictionary<string, FilterSettings>(StringComparer.Ordinal);
        }
    }

    private static FilterSettings Normalize(FilterSettings settings, FilterSettings fallback)
    {
        double threshold = settings.SemanticThreshold is >= 0.5 and <= 1.0
            ? settings.SemanticThreshold
            : fallback.SemanticThreshold;

        return settings with
        {
            Extensions = settings.Extensions ?? string.Empty,
            MinimumSizeText = settings.MinimumSizeText ?? string.Empty,
            MaximumSizeText = settings.MaximumSizeText ?? string.Empty,
            ExcludedFolders = settings.ExcludedFolders ?? string.Empty,
            SemanticThreshold = threshold
        };
    }
}
