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

    public long FileSize
    {
        get
        {
            try
            {
                return File.Exists(_path) ? new FileInfo(_path).Length : 0;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return 0;
            }
        }
    }

    public static string DefaultPath
    {
        get
        {
            return CachePaths.ScanCacheFile;
        }
    }

    public byte[] GetOrCompute(string algorithm, string path, Func<string, byte[]> compute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentNullException.ThrowIfNull(compute);

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

            string json = JsonSerializer.Serialize(_entries, JsonOptions);
            AtomicFile.WriteAllText(_path, json);
            _dirty = false;
        }
    }

    private Dictionary<string, ScanCacheEntry> Load()
    {
        Dictionary<string, ScanCacheEntry> empty = new(PathUtilities.PathComparer);

        if (!File.Exists(_path))
        {
            return empty;
        }

        try
        {
            string json = File.ReadAllText(_path);
            Dictionary<string, ScanCacheEntry>? loaded =
                JsonSerializer.Deserialize<Dictionary<string, ScanCacheEntry>>(json, JsonOptions);

            if (loaded is null)
            {
                return empty;
            }

            Dictionary<string, ScanCacheEntry> entries = new(PathUtilities.PathComparer);

            foreach ((string path, ScanCacheEntry? entry) in loaded)
            {
                // Hand-edited or truncated caches can contain null values. Ignore the
                // unusable entries instead of failing the whole scan later.
                if (!string.IsNullOrEmpty(path) &&
                    entry is not null &&
                    entry.Algorithm is not null &&
                    entry.Hash is not null)
                {
                    entries[path] = entry;
                }
            }

            return entries;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return empty;
        }
    }
}
