using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileCompare;

namespace ImageCompare;

public sealed record CacheEntry(
    string Kind,
    string Name,
    string Path,
    int? Dimensions,
    int Entries,
    long Bytes);

public sealed record CacheSnapshot(IReadOnlyList<CacheEntry> Entries, long TotalBytes);

public sealed record CacheCleanupResult(int Files, long Bytes);

/// <summary>
/// Inspects, prunes and clears the data caches. Shared by the CLI and the
/// desktop settings panel so both report exactly the same numbers.
/// </summary>
public static class CacheInspector
{
    public static CacheSnapshot Inspect(string directory, string? modelsDirectory = null)
    {
        List<CacheEntry> entries = new();
        string scanPath = Path.Combine(directory, "scan-cache.json");

        if (File.Exists(scanPath))
        {
            ScanCache scan = new(scanPath);
            entries.Add(new CacheEntry("scan", "hashes", scanPath, null, scan.Count, scan.FileSize));
        }

        foreach (string path in EnumerateCacheFiles(directory, "embeddings-*.bin"))
        {
            if (!EmbeddingCache.TryReadHeader(path, out int dimensions, out int count))
            {
                continue;
            }

            entries.Add(new CacheEntry(
                "embeddings",
                ExtractCacheName(path, "embeddings-"),
                path,
                dimensions,
                count,
                new FileInfo(path).Length));
        }

        foreach (string path in EnumerateCacheFiles(directory, "quality-*.bin"))
        {
            if (!EmbeddingCache.TryReadHeader(path, out _, out int count))
            {
                continue;
            }

            entries.Add(new CacheEntry(
                "quality",
                ExtractCacheName(path, "quality-"),
                path,
                1,
                count,
                new FileInfo(path).Length));
        }

        string models = modelsDirectory ?? CachePaths.ModelsDirectory;

        if (Directory.Exists(models))
        {
            foreach (string path in Directory.EnumerateDirectories(models))
            {
                long bytes = Directory
                    .EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);

                entries.Add(new CacheEntry(
                    "models",
                    Path.GetFileName(path),
                    path,
                    null,
                    0,
                    bytes));
            }
        }

        return new CacheSnapshot(entries, entries.Sum(entry => entry.Bytes));
    }

    public static int Prune(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        int removed = 0;
        string scanPath = Path.Combine(directory, "scan-cache.json");

        if (File.Exists(scanPath))
        {
            ScanCache scan = new(scanPath);
            removed += scan.PruneMissingFiles();
            scan.Save();
        }

        foreach (string path in EnumerateCacheFiles(directory, "embeddings-*.bin"))
        {
            if (!EmbeddingCache.TryReadHeader(path, out int dimensions, out _))
            {
                continue;
            }

            EmbeddingCache cache = new(path, dimensions);
            removed += cache.PruneMissingFiles();
            cache.Save();
        }

        foreach (string path in EnumerateCacheFiles(directory, "quality-*.bin"))
        {
            EmbeddingCache cache = new(path, dimensions: 1);
            removed += cache.PruneMissingFiles();
            cache.Save();
        }

        return removed;
    }

    public static CacheCleanupResult Clear(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new CacheCleanupResult(0, 0);
        }

        int files = 0;
        long bytes = 0;

        foreach (string path in EnumerateCacheFiles(directory, "scan-cache.json")
                     .Concat(EnumerateCacheFiles(directory, "embeddings-*.bin"))
                     .Concat(EnumerateCacheFiles(directory, "quality-*.bin")))
        {
            bytes += new FileInfo(path).Length;
            File.Delete(path);
            files++;
        }

        return new CacheCleanupResult(files, bytes);
    }

    private static IEnumerable<string> EnumerateCacheFiles(string directory, string pattern)
    {
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, pattern)
            : Array.Empty<string>();
    }

    private static string ExtractCacheName(string path, string prefix)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? name[prefix.Length..]
            : name;
    }
}
