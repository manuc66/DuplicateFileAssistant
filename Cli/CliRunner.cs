using System.Security.Cryptography;
using System.Text.Json;
using FileCompare;
using ImageCompare;
using ImageCompare.Onnx;

namespace DuplicateFileAssistant.Cli;

public static class CliRunner
{
    private const string DefaultImageExtensions = "jpg,jpeg,png,gif,bmp,webp,ico";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args, TextWriter output, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            output.WriteLine("Commands: duplicates, similar-images, cache, empty-files, big-files, temporary, invalid-symlinks, empty-folders");
            output.WriteLine("Options: --path <dir> [--json] [--apply] [--trash <dir>] [--exclude <dir>]... [--max <n>] [--algorithm sha256|md5]");
            output.WriteLine("similar-images: [--threshold <bits>] [--min-size <bytes>] [--extensions <ext,...>] [--semantic] [--embedding <id>] [--semantic-threshold <0..1>] [--semantic-margin <0..1>]");
            output.WriteLine("                [--keeper] [--keeper-model <id>] [--model-dir <dir>] [--no-download] [--no-cache] [--prune-cache] [--cache-path <file>] [--embedding-cache <file>] [--quality-cache <file>]");
            output.WriteLine("cache: [--json] [--prune] [--clear --apply] [--cache-dir <dir>] [--model-dir <dir>]");
            return args.Length == 0 ? 1 : 0;
        }

        string command = args[0].ToLowerInvariant();
        CliOptions options = CliOptions.Parse(args.Skip(1));

        if (command == "cache")
        {
            try
            {
                return RunCacheCommand(options, output);
            }
            catch (Exception exception)
            {
                output.WriteLine($"Error: {exception.Message}");
                return 2;
            }
        }

        if (!options.TryGetSingle("path", out string? root) || string.IsNullOrWhiteSpace(root))
        {
            output.WriteLine("Missing required option: --path <dir>");
            return 1;
        }

        bool json = options.HasFlag("json");
        bool apply = options.HasFlag("apply");
        string trashPath = options.GetSingle("trash") ?? "~/DuplicateTrash";
        string[] excludedFolders = options.GetMany("exclude").Append(trashPath).ToArray();
        FileSearchFilter filter = FileSearchFilter.FromText(0, null, string.Join(";", excludedFolders));

        try
        {
            return command switch
            {
                "duplicates" => await RunDuplicatesAsync(root, options, filter, apply, trashPath, json, output, cancellationToken),
                "similar-images" => await RunSimilarImagesAsync(root, options, excludedFolders, json, output, cancellationToken),
                "empty-files" => await RunFileFinderAsync("empty-files", root, filter,
                    () => EmptyFileFinder.Find(root, filter, cancellationToken), apply, trashPath, json, output, cancellationToken),
                "big-files" => await RunFileFinderAsync("big-files", root, filter,
                    () => BigFileFinder.Find(root, options.GetInt("max", 100), filter, cancellationToken),
                    apply, trashPath, json, output, cancellationToken),
                "temporary" => await RunFileFinderAsync("temporary", root, filter,
                    () => TemporaryFileFinder.Find(root, filter, cancellationToken),
                    apply, trashPath, json, output, cancellationToken),
                "invalid-symlinks" => await RunInvalidLinksAsync(root, filter, apply, trashPath, json, output, cancellationToken),
                "empty-folders" => await RunEmptyFoldersAsync(root, filter, apply, json, output, cancellationToken),
                _ => Unknown(command, output)
            };
        }
        catch (Exception ex)
        {
            output.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    private static int RunCacheCommand(CliOptions options, TextWriter output)
    {
        bool json = options.HasFlag("json");
        bool prune = options.HasFlag("prune");
        bool clear = options.HasFlag("clear");
        string directory = Path.GetFullPath(options.GetSingle("cache-dir") ?? CachePaths.RootDirectory);

        if (clear)
        {
            if (!options.HasFlag("apply"))
            {
                output.WriteLine("Refusing to delete caches without --apply.");
                return 1;
            }

            CacheCleanupResult cleanup = CacheInspector.Clear(directory);

            if (json)
            {
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "cache",
                    directory,
                    action = "clear",
                    filesDeleted = cleanup.Files,
                    bytesFreed = cleanup.Bytes
                }, JsonOptions));
            }
            else
            {
                output.WriteLine(
                    $"cache: {directory} (cleared {cleanup.Files} file(s), {FormatBytes(cleanup.Bytes)})");
            }

            return 0;
        }

        int pruned = prune ? CacheInspector.Prune(directory) : 0;
        CacheSnapshot snapshot = CacheInspector.Inspect(directory, options.GetSingle("model-dir"));

        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(new
            {
                command = "cache",
                directory,
                pruned,
                entries = snapshot.Entries.Select(entry => new
                {
                    kind = entry.Kind,
                    name = entry.Name,
                    path = entry.Path,
                    dimensions = entry.Dimensions,
                    entries = entry.Entries,
                    bytes = entry.Bytes
                }).ToArray(),
                totalBytes = snapshot.TotalBytes
            }, JsonOptions));
            return 0;
        }

        output.WriteLine($"cache: {directory}");

        if (prune)
        {
            output.WriteLine($"Pruned {pruned} missing file(s).");
        }

        foreach (CacheEntry entry in snapshot.Entries)
        {
            output.WriteLine(
                $"  {entry.Kind,-10} {entry.Name,-16} {entry.Entries,8} entries " +
                $"{FormatBytes(entry.Bytes),10}  {entry.Path}");
        }

        output.WriteLine($"Total: {FormatBytes(snapshot.TotalBytes)}");
        return 0;
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:F1} MB",
            _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB"
        };
    }

    private static async Task<int> RunDuplicatesAsync(string root, CliOptions options, FileSearchFilter filter, bool apply,
        string trashPath, bool json, TextWriter output, CancellationToken cancellationToken)
    {
        string algorithm = options.GetSingle("algorithm")?.ToLowerInvariant() ?? "sha256";
        ScanCache? cache = options.HasFlag("no-cache")
            ? null
            : new ScanCache(options.GetSingle("cache-path") ?? ScanCache.DefaultPath);

        if (options.HasFlag("prune-cache"))
        {
            cache?.PruneMissingFiles();
        }

        Dictionary<HashKey, HashSet<DuplicateFile>> groups;

        try
        {
            groups = DupProvider.FindDuplicateByHash(
                root,
                SearchOption.AllDirectories,
                fileInfo => !fileInfo.IsLink() && !fileInfo.IsFifoFile(),
                TextWriter.Null,
                _ => { },
                path => cache is null
                    ? HashFile(path, algorithm)
                    : cache.GetOrCompute(algorithm, path, filePath => HashFile(filePath, algorithm)),
                cancellationToken,
                filter.MatchesPath);
        }
        finally
        {
            TrySave(() => cache?.Save());
        }

        List<object> results = groups.Select(group => new
        {
            size = group.Value.First().Length,
            hash = group.Key.ToString(),
            files = group.Value.Select(file => file.Path).OrderBy(path => path, PathUtilities.PathComparer).ToArray()
        }).Cast<object>().ToList();

        int affected = groups.Sum(group => Math.Max(0, group.Value.Count - 1));

        if (apply)
        {
            Trash trash = new(trashPath);
            affected = await DeleteFilesAsync(groups.SelectMany(group =>
                group.Value.OrderBy(file => file.Path, PathUtilities.PathComparer).Skip(1)), trash, output,
                cancellationToken);
        }

        Write(output, json, command: "duplicates", root, apply, affected, results);
        return 0;
    }

    private static async Task<int> RunSimilarImagesAsync(string root, CliOptions options,
        string[] excludedFolders, bool json, TextWriter output, CancellationToken cancellationToken)
    {
        if (options.HasFlag("apply"))
        {
            output.WriteLine("similar-images is report-only: no file is ever deleted by this command.");
            return 1;
        }

        int threshold = options.GetInt("threshold", SimilarImageFinder.DefaultMaxDistance);
        long minimumSize = options.GetLong("min-size", 10 * 1024);
        string extensions = options.GetSingle("extensions") ?? DefaultImageExtensions;
        double semanticThreshold = options.GetDouble("semantic-threshold", 0.80);
        double semanticMargin = options.GetDouble("semantic-margin", 0.05);
        FileSearchFilter filter =
            FileSearchFilter.FromText(minimumSize, extensions, string.Join(";", excludedFolders));

        bool noCache = options.HasFlag("no-cache");
        ScanCache? cache = noCache
            ? null
            : new ScanCache(options.GetSingle("cache-path") ?? ScanCache.DefaultPath);

        string? embeddingId = options.GetSingle("embedding");

        if (options.HasFlag("semantic"))
        {
            embeddingId ??= "dinov2-small";
        }

        string? qualityId = options.GetSingle("keeper-model");
        bool allowDownload = !options.HasFlag("no-download");
        TextWriter modelLog = json ? Console.Error : output;

        IImageEmbeddingProvider? embeddings = null;
        IQualityScorer? quality = null;
        QualityCache? qualityCache = null;
        EmbeddingCache? embeddingCache = null;
        SimilarImageScanResult result;

        try
        {
            OnnxModelStore? store = embeddingId is null && qualityId is null
                ? null
                : new OnnxModelStore(options.GetSingle("model-dir"));

            if (embeddingId is not null)
            {
                embeddings = await OnnxProviders.CreateImageEmbeddingAsync(
                    embeddingId,
                    store!,
                    allowDownload,
                    message => modelLog.WriteLine(message),
                    cancellationToken);

                if (!noCache)
                {
                    string embeddingCachePath = options.GetSingle("embedding-cache") ??
                                                EmbeddingCache.DefaultPath(embeddings.Id);

                    embeddingCache = new EmbeddingCache(embeddingCachePath, embeddings.Dimensions);
                }
            }

            if (qualityId is not null)
            {
                quality = await OnnxProviders.CreateQualityScorerAsync(
                    qualityId,
                    store!,
                    allowDownload,
                    message => modelLog.WriteLine(message),
                    cancellationToken);
            }
            else if (options.HasFlag("keeper"))
            {
                quality = new HeuristicQualityScorer();
            }

            if (quality is not null && !noCache)
            {
                string qualityCachePath = options.GetSingle("quality-cache") ??
                                          QualityCache.DefaultPath(quality.Id);

                qualityCache = new QualityCache(qualityCachePath, quality.Id);
                quality = new CachedQualityScorer(quality, qualityCache);
            }

            if (options.HasFlag("prune-cache"))
            {
                cache?.PruneMissingFiles();
                embeddingCache?.PruneMissingFiles();
                qualityCache?.PruneMissingFiles();
            }

            SimilarImageOptions scanOptions = new()
            {
                MaxDistance = threshold,
                Embeddings = embeddings,
                SemanticThreshold = semanticThreshold,
                SemanticMargin = semanticMargin,
                Quality = quality
            };

            result = SimilarImageFinder.Find(
                root,
                SearchOption.AllDirectories,
                filter,
                scanOptions,
                json ? TextWriter.Null : output,
                updateProgress: null,
                cancellationToken,
                cache: cache,
                embeddingCache: embeddingCache);
        }
        finally
        {
            TrySave(() => cache?.Save());
            TrySave(() => embeddingCache?.Save());
            TrySave(() => qualityCache?.Save());
            embeddings?.Dispose();
            quality?.Dispose();
        }

        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(new
            {
                command = "similar-images",
                root,
                applied = false,
                affected = result.RedundantCount,
                threshold,
                semanticModel = embeddings?.Id,
                semanticThreshold,
                semanticMargin,
                keeperModel = quality?.Id,
                hashed = result.HashedCount,
                embedded = result.EmbeddedCount,
                skipped = result.Failures.Count,
                elapsedSeconds = Math.Round(result.Elapsed.TotalSeconds, 3),
                results = result.Groups.Select(group => new
                {
                    kind = group.Kind == SimilarImageKind.Semantic ? "semantic" : "perceptual",
                    maximumDistance = group.MaximumDistance,
                    minimumSimilarity = group.MinimumSimilarity,
                    leader = group.Leader.Path,
                    keeper = group.Keeper.Path,
                    keeperQuality = group.KeeperQuality,
                    files = group.Images.Select(member => member.Image.Path).ToArray(),
                    distances = group.Images.Select(member => member.Distance).ToArray(),
                    similarities = group.Images.Select(member => member.Similarity).ToArray(),
                    qualities = group.Images.Select(member => member.Quality).ToArray()
                }).ToArray(),
                failures = result.Failures.Select(failure => new
                {
                    path = failure.Path,
                    error = failure.Error,
                    stage = failure.Stage.ToString().ToLowerInvariant()
                }).ToArray()
            }, JsonOptions));

            return 0;
        }

        output.WriteLine(
            $"similar-images: {root} (dry-run, threshold {threshold}, semantic {embeddings?.Id ?? "off"}, keeper {quality?.Id ?? "off"})");
        output.WriteLine(
            $"Hashed: {result.HashedCount}, embedded: {result.EmbeddedCount}, " +
            $"skipped: {result.Failures.Count}, affected: {result.RedundantCount}");

        int index = 0;

        foreach (SimilarImageGroup group in result.Groups)
        {
            string similarity = group.MinimumSimilarity is double value
                ? $", minimum similarity {value:F3}"
                : string.Empty;

            output.WriteLine(
                $"Group {++index} ({group.Kind.ToString().ToLowerInvariant()}, " +
                $"maximum distance {group.MaximumDistance}{similarity}):");

            foreach (SimilarImageMatch member in group.Images)
            {
                bool isKeeper = PathUtilities.PathComparer.Equals(member.Image.Path, group.Keeper.Path);
                bool isLeader = PathUtilities.PathComparer.Equals(member.Image.Path, group.Leader.Path);
                string marker = isKeeper ? "keep" : isLeader ? "lead" : "    ";
                string qualityText = member.Quality is double score ? $" quality {score:F3}" : string.Empty;
                string similarityText = member.Similarity is double memberSimilarity
                    ? $" similarity {memberSimilarity:F3}"
                    : string.Empty;

                output.WriteLine(
                    $"  {marker} dHash {member.Distance,2}{similarityText}{qualityText}  {member.Image.Path}");
            }
        }

        foreach (SimilarImageFailure failure in result.Failures)
        {
            output.WriteLine($"Skipped ({failure.Stage.ToString().ToLowerInvariant()}): {failure.Path}: {failure.Error}");
        }

        return 0;
    }

    private static void TrySave(Action save)
    {
        try
        {
            save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A read-only or missing cache location must not fail the scan.
        }
    }

    private static async Task<int> RunFileFinderAsync(string command, string root, FileSearchFilter filter,
        Func<IReadOnlyList<DuplicateFile>> finder, bool apply, string trashPath, bool json, TextWriter output,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DuplicateFile> files = finder();
        var results = files.Select(file => new { path = file.Path, length = file.Length }).Cast<object>().ToList();
        int affected = files.Count;

        if (apply && files.Count > 0)
        {
            Trash trash = new(trashPath);
            affected = await DeleteFilesAsync(files, trash, output, cancellationToken);
        }

        Write(output, json, command, root, apply, affected, results);
        return 0;
    }

    private static async Task<int> RunInvalidLinksAsync(string root, FileSearchFilter filter, bool apply,
        string trashPath, bool json, TextWriter output, CancellationToken cancellationToken)
    {
        IReadOnlyList<InvalidSymbolicLink> links = InvalidSymbolicLinkFinder.Find(root, filter, cancellationToken);
        var results = links.Select(link => new { path = link.Path, target = link.Target }).Cast<object>().ToList();

        if (apply && links.Count > 0)
        {
            Trash trash = new(trashPath);

            foreach (InvalidSymbolicLink link in links)
            {
                await trash.DeleteSymbolicLinkAsync(link.Path, cancellationToken);
            }
        }

        Write(output, json, "invalid-symlinks", root, apply, links.Count, results);
        return 0;
    }

    private static Task<int> RunEmptyFoldersAsync(string root, FileSearchFilter filter, bool apply, bool json,
        TextWriter output, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> directories = EmptyDirectoryService.FindEmptyDirectories(
            root,
            cancellationToken,
            filter.MatchesDirectory);

        var results = directories.Select(directory => new { path = directory }).Cast<object>().ToList();
        int affected = directories.Count;

        if (apply)
        {
            affected = EmptyDirectoryService.DeleteEmptyDirectories(directories, cancellationToken);
        }

        Write(output, json, "empty-folders", root, apply, affected, results);
        return Task.FromResult(0);
    }

    private static async Task<int> DeleteFilesAsync(IEnumerable<DuplicateFile> files, Trash trash, TextWriter output,
        CancellationToken cancellationToken)
    {
        int deleted = 0;

        foreach (DuplicateFile file in files)
        {
            try
            {
                await trash.DeleteAsync(file.Path, file.Length, file.LastWriteTimeUtc, cancellationToken);
                deleted++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                output.WriteLine($"Failed: {file.Path}: {ex.Message}");
            }
        }

        return deleted;
    }

    private static void Write(TextWriter output, bool json, string command, string root, bool applied, int affected,
        IReadOnlyList<object> results)
    {
        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(new
            {
                command,
                root,
                applied,
                affected,
                results
            }, JsonOptions));
            return;
        }

        output.WriteLine($"{command}: {root} ({(applied ? "applied" : "dry-run")})");
        output.WriteLine($"Affected: {affected}");

        foreach (object result in results)
        {
            output.WriteLine(result);
        }
    }

    private static int Unknown(string command, TextWriter output)
    {
        output.WriteLine($"Unknown command: {command}");
        return 1;
    }


    private static byte[] HashFile(string path, string algorithm)
    {
        using FileStream stream = File.OpenRead(path);

        return algorithm == "md5"
            ? MD5.HashData(stream)
            : SHA256.HashData(stream);
    }
}
