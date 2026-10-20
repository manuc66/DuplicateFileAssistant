using System.Text.Json;

namespace FileCompare;

public sealed record ScanCacheEntry(
    string Algorithm,
    long Length,
    DateTime LastWriteTimeUtc,
    string Hash);

public sealed class ScanCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly object _sync = new();
    private readonly string _path;
    private Dictionary<string, ScanCacheEntry> _entries;

    public ScanCache(string path)
    {
        _path = PathUtilities.ExpandPath(path);
        _entries = Load();
    }

    public static string DefaultPath
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.GetTempPath();
            }

            return Path.Combine(root, "DuplicateFileAssistant", "scan-cache.json");
        }
    }

    public byte[] GetOrCompute(string algorithm, string path, Func<string, byte[]> compute)
    {
        FileInfo fileInfo = new(path);

        lock (_sync)
        {
            if (_entries.TryGetValue(path, out ScanCacheEntry? entry) &&
                entry.Algorithm == algorithm &&
                entry.Length == fileInfo.Length &&
                entry.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc)
            {
                try
                {
                    return Convert.FromHexString(entry.Hash);
                }
                catch (FormatException)
                {
                }
            }
        }

        byte[] hash = compute(path);

        lock (_sync)
        {
            _entries[path] = new ScanCacheEntry(
                algorithm,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc,
                Convert.ToHexStringLower(hash));
        }

        return hash;
    }

    public void Save()
    {
        lock (_sync)
        {
            string? directory = Path.GetDirectoryName(_path);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(_entries, JsonOptions);
            string temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, overwrite: true);
        }
    }

    private Dictionary<string, ScanCacheEntry> Load()
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, ScanCacheEntry>();
        }

        try
        {
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<Dictionary<string, ScanCacheEntry>>(json, JsonOptions)
                   ?? new Dictionary<string, ScanCacheEntry>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Dictionary<string, ScanCacheEntry>();
        }
    }
}
