using System.Text.Json;

namespace FileCompare;

/// <summary>
/// Remembers the last folder used by a scan. Missing paths and corrupt files fall
/// back to the configured default, so a deleted folder never blocks startup.
/// </summary>
public sealed class LastFolderStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly string _settingsFile;
    private LastFolderSettings _current;

    public LastFolderStore(string settingsFile, LastFolderSettings defaults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFile);
        ArgumentNullException.ThrowIfNull(defaults);

        _settingsFile = PathUtilities.ExpandPath(settingsFile);
        _current = Load(Normalize(defaults.LastFolder, defaults.LastFolder));
    }

    public event EventHandler? Changed;

    public LastFolderSettings Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public void Save(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        string normalized;

        try
        {
            normalized = PathUtilities.ExpandPath(folder);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            return;
        }

        if (!Directory.Exists(normalized))
        {
            return;
        }

        lock (_sync)
        {
            if (string.Equals(_current.LastFolder, normalized, PathUtilities.PathComparison))
            {
                return;
            }
        }

        LastFolderSettings settings = new(normalized);
        string? directory = Path.GetDirectoryName(_settingsFile);

        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        string temporaryPath = _settingsFile + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _settingsFile, overwrite: true);

        lock (_sync)
        {
            _current = settings;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private LastFolderSettings Load(string fallback)
    {
        if (!File.Exists(_settingsFile))
        {
            return new LastFolderSettings(fallback);
        }

        try
        {
            string json = File.ReadAllText(_settingsFile);
            LastFolderSettings? loaded = JsonSerializer.Deserialize<LastFolderSettings>(json, JsonOptions);
            return new LastFolderSettings(Normalize(loaded?.LastFolder ?? fallback, fallback));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or JsonException)
        {
            return new LastFolderSettings(fallback);
        }
    }

    private static string Normalize(string folder, string fallback)
    {
        string? expanded = TryExpand(folder);

        if (expanded is not null && Directory.Exists(expanded))
        {
            return expanded;
        }

        string? expandedFallback = TryExpand(fallback);

        if (expandedFallback is not null && Directory.Exists(expandedFallback))
        {
            return expandedFallback;
        }

        return Environment.CurrentDirectory;
    }

    private static string? TryExpand(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        try
        {
            return PathUtilities.ExpandPath(folder);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            return null;
        }
    }
}
