namespace FileCompare;

/// <summary>
/// Central location for every cache written by the application. Keeping the
/// paths in one place makes the caches easy to inspect, prune or clear.
/// </summary>
public static class CachePaths
{
    public static string RootDirectory => ApplicationPaths.RootDirectory;

    public static string ModelsDirectory => Path.Combine(RootDirectory, "models");

    public static string ScanCacheFile => Path.Combine(RootDirectory, "scan-cache.json");

    public static string EmbeddingCacheFile(string modelId)
    {
        return Path.Combine(RootDirectory, $"embeddings-{Sanitize(modelId)}.bin");
    }

    public static string QualityCacheFile(string scorerId)
    {
        return Path.Combine(RootDirectory, $"quality-{Sanitize(scorerId)}.bin");
    }

    public static string Sanitize(string value)
    {
        return string.Concat(value.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    }
}
