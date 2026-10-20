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
    private bool _dirty;

    public ScanCache(string path)
    {
        _path = PathUtilities.ExpandPath(path);
        _entries = Load();
    }

    public string FilePath => _path;

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _entries.Count;
            }
        }
    }

    public long FileSize => File.Exists(_path) ? new FileInfo(_path).Length : 0;

    public static string DefaultPath
    {
        get
        {
            return CachePaths.ScanCacheFile;
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
            _dirty = true;
        }

        return hash;
    }

    public int PruneMissingFiles()
    {
        lock (_sync)
        {
            string[] missing = _entries.Keys.Where(path => !File.Exists(path)).ToArray();

            foreach (string path in missing)
            {
                _entries.Remove(path);
            }

            if (missing.Length > 0)
            {
                _dirty = true;
            }

            return missing.Length;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            if (_entries.Count == 0)
            {
                return;
            }

            _entries.Clear();
            _dirty = true;
        }
    }

    public void Save()
    {
        lock (_sync)
        {
            if (!_dirty)
            {
                return;
            }

            string? directory = Path.GetDirectoryName(_path);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(_entries, JsonOptions);
            string temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, overwrite: true);
            _dirty = false;
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
