using System.Text;
using FileCompare;

namespace ImageCompare;

/// <summary>
/// Binary, model-specific cache for embeddings. Entries are invalidated when the
/// file length or its last write time changes.
/// </summary>
public sealed class EmbeddingCache
{
    private const uint Magic = 0x4943454D; // "ICEM"
    private const int Version = 1;

    private readonly object _sync = new();
    private readonly string _path;
    private readonly int _dimensions;
    private Dictionary<string, Entry> _entries;
    private bool _dirty;

    public EmbeddingCache(string path, int dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);

        _path = PathUtilities.ExpandPath(path);
        _dimensions = dimensions;
        _entries = Load();
    }

    public string FilePath => _path;

    public int Dimensions => _dimensions;

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

    public static string DefaultPath(string modelId)
    {
        return CachePaths.EmbeddingCacheFile(modelId);
    }

    public float[] GetOrCompute(string path, Func<string, float[]> compute)
    {
        FileInfo fileInfo = new(path);

        lock (_sync)
        {
            if (_entries.TryGetValue(path, out Entry? entry) &&
                entry.Vector.Length == _dimensions &&
                entry.Length == fileInfo.Length &&
                entry.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc)
            {
                return entry.Vector;
            }
        }

        float[] vector = compute(path);

        if (vector.Length != _dimensions)
        {
            throw new InvalidDataException(
                $"Expected an embedding of {_dimensions} values but got {vector.Length}: {path}");
        }

        lock (_sync)
        {
            _entries[path] = new Entry(fileInfo.Length, fileInfo.LastWriteTimeUtc, vector);
            _dirty = true;
        }

        return vector;
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

            string temporaryPath = _path + ".tmp";

            using (FileStream stream = File.Create(temporaryPath))
            using (BinaryWriter writer = new(stream, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(_dimensions);
                writer.Write(_entries.Count);

                foreach ((string path, Entry entry) in _entries)
                {
                    writer.Write(path);
                    writer.Write(entry.Length);
                    writer.Write(entry.LastWriteTimeUtc.Ticks);

                    foreach (float value in entry.Vector)
                    {
                        writer.Write(value);
                    }
                }
            }

            File.Move(temporaryPath, _path, overwrite: true);
            _dirty = false;
        }
    }

    public static bool TryReadHeader(string path, out int dimensions, out int count)
    {
        dimensions = 0;
        count = 0;

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new(stream, Encoding.UTF8);

            if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version)
            {
                return false;
            }

            dimensions = reader.ReadInt32();
            count = reader.ReadInt32();

            return dimensions > 0 && count >= 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or EndOfStreamException or FormatException)
        {
            return false;
        }
    }

    private Dictionary<string, Entry> Load()
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, Entry>(PathUtilities.PathComparer);
        }

        try
        {
            using FileStream stream = File.OpenRead(_path);
            using BinaryReader reader = new(stream, Encoding.UTF8);

            if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version)
            {
                return new Dictionary<string, Entry>(PathUtilities.PathComparer);
            }

            int dimensions = reader.ReadInt32();
            int count = reader.ReadInt32();

            if (dimensions != _dimensions || count < 0)
            {
                return new Dictionary<string, Entry>(PathUtilities.PathComparer);
            }

            Dictionary<string, Entry> entries = new(count, PathUtilities.PathComparer);

            for (int index = 0; index < count; index++)
            {
                string path = reader.ReadString();
                long length = reader.ReadInt64();
                long ticks = reader.ReadInt64();
                float[] vector = new float[dimensions];

                for (int valueIndex = 0; valueIndex < dimensions; valueIndex++)
                {
                    vector[valueIndex] = reader.ReadSingle();
                }

                entries[path] = new Entry(length, new DateTime(ticks, DateTimeKind.Utc), vector);
            }

            return entries;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or EndOfStreamException or FormatException)
        {
            return new Dictionary<string, Entry>(PathUtilities.PathComparer);
        }
    }

    private sealed record Entry(long Length, DateTime LastWriteTimeUtc, float[] Vector);
}
