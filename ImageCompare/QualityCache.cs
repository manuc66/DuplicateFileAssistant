using FileCompare;

namespace ImageCompare;

/// <summary>
/// Disk cache for quality scores. The key is the file path, its length and its
/// last write time; the scorer id is part of the cache file name so that the
/// heuristic and model-based scores never mix.
/// </summary>
public sealed class QualityCache
{
    private readonly EmbeddingCache _cache;

    public QualityCache(string path, string scorerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(scorerId);

        ScorerId = scorerId;
        _cache = new EmbeddingCache(path, dimensions: 1);
    }

    public string ScorerId { get; }

    public string FilePath => _cache.FilePath;

    public int Count => _cache.Count;

    public long FileSize => _cache.FileSize;

    public double GetOrCompute(string path, Func<string, double> compute)
    {
        float[] value = _cache.GetOrCompute(path, filePath => new[] { (float)compute(filePath) });
        return value[0];
    }

    public int PruneMissingFiles()
    {
        return _cache.PruneMissingFiles();
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public void Save()
    {
        _cache.Save();
    }

    public static string DefaultPath(string scorerId)
    {
        return CachePaths.QualityCacheFile(scorerId);
    }
}
