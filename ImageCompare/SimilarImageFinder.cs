using System.Collections.Concurrent;
using System.Diagnostics;
using FileCompare;

namespace ImageCompare;

/// <summary>
/// Finds groups of near-identical images. A first, cheap dHash pass groups
/// resized and recompressed copies. When an <see cref="IImageEmbeddingProvider"/>
/// is supplied, a second pass looks for semantically similar images (crops,
/// edits) and attaches them to the same groups. An optional
/// <see cref="IQualityScorer"/> then suggests which copy to keep.
///
/// Grouping is leader-based: every member is compared to the leader of an
/// existing group, never to another member. That keeps groups star-shaped and
/// prevents a chain of loose matches from merging unrelated pictures.
/// </summary>
public static class SimilarImageFinder
{
    public const int DefaultMaxDistance = 8;

    private const int DefaultMaxDegreeOfParallelism = 8;

    public static SimilarImageScanResult Find(
        string root,
        SearchOption searchOption,
        FileSearchFilter filter,
        SimilarImageOptions? options,
        TextWriter progress,
        Action<int>? updateProgress,
        CancellationToken cancellationToken,
        Func<string, bool>? pathFilter = null,
        ScanCache? cache = null,
        int maxDegreeOfParallelism = 0,
        EmbeddingCache? embeddingCache = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(filter);

        options ??= new SimilarImageOptions();

        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxDistance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MaxDistance, PerceptualHasher.HashBits);

        if (options.SemanticThreshold is < 0 or > 1 ||
            options.SemanticMargin is < 0 or > 1 ||
            options.SemanticStrongMatch is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options),
                "Semantic thresholds and margins must be between 0 and 1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(options.SimHashMaxDistance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.SimHashMaxDistance, SimHash.Bits);
        ArgumentOutOfRangeException.ThrowIfNegative(options.BruteForceEmbeddingLimit);

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory not found: {root}");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        List<DuplicateFile> candidates =
            CollectCandidates(root, searchOption, filter, pathFilter, cancellationToken);

        updateProgress?.Invoke(0);
        progress.WriteLine($"Hashing {candidates.Count} image(s) from {root}...");

        ParallelOptions parallelOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = maxDegreeOfParallelism > 0
                ? maxDegreeOfParallelism
                : Math.Min(Environment.ProcessorCount, DefaultMaxDegreeOfParallelism)
        };

        ConcurrentBag<(DuplicateFile File, PerceptualHash Hash)> hashed = new();
        ConcurrentBag<SimilarImageFailure> failures = new();
        int processed = 0;

        Parallel.ForEach(candidates, parallelOptions, file =>
        {
            try
            {
                PerceptualHash hash = cache is null
                    ? PerceptualHasher.Compute(file.Path)
                    : ComputeWithCache(cache, file.Path);

                hashed.Add((file, hash));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new SimilarImageFailure(
                    file.Path,
                    exception.Message,
                    SimilarImageFailureStage.Hash));
            }
            finally
            {
                int done = Interlocked.Increment(ref processed);

                if (done % 100 == 0)
                {
                    updateProgress?.Invoke((int)Math.Min(99L, 100L * done / Math.Max(1, candidates.Count)));
                }
            }
        });

        List<(DuplicateFile File, PerceptualHash Hash)> ordered = hashed
            .OrderBy(item => item.File.Path, PathUtilities.PathComparer)
            .ToList();

        float[]?[]? embeddings = ComputeEmbeddings(
            ordered,
            options,
            embeddingCache,
            parallelOptions,
            failures,
            progress,
            cancellationToken);

        int embeddedCount = embeddings?.Count(vector => vector is not null) ?? 0;

        List<SimilarImageGroup> groups = BuildGroups(
            ordered,
            embeddings,
            options,
            parallelOptions,
            failures,
            progress,
            cancellationToken);

        stopwatch.Stop();
        updateProgress?.Invoke(100);

        SimilarImageFailure[] orderedFailures = failures
            .OrderBy(failure => failure.Stage)
            .ThenBy(failure => failure.Path, PathUtilities.PathComparer)
            .ToArray();

        progress.WriteLine(
            $"Hashed {ordered.Count}/{candidates.Count} image(s) in {stopwatch.Elapsed.TotalSeconds:F1}s; " +
            $"{groups.Count} similar group(s), {orderedFailures.Length} skipped.");

        return new SimilarImageScanResult(
            groups,
            orderedFailures,
            ordered.Count,
            embeddedCount,
            stopwatch.Elapsed);
    }

    private static float[]?[]? ComputeEmbeddings(
        List<(DuplicateFile File, PerceptualHash Hash)> images,
        SimilarImageOptions options,
        EmbeddingCache? embeddingCache,
        ParallelOptions parallelOptions,
        ConcurrentBag<SimilarImageFailure> failures,
        TextWriter progress,
        CancellationToken cancellationToken)
    {
        if (options.Embeddings is null)
        {
            return null;
        }

        float[]?[] embeddings = new float[]?[images.Count];
        int processed = 0;
        object progressLock = new();

        progress.WriteLine($"Embedding {images.Count} image(s) with {options.Embeddings.Id}...");

        Parallel.For(0, images.Count, parallelOptions, index =>
        {
            string path = images[index].File.Path;

            try
            {
                float[] vector = embeddingCache is null
                    ? options.Embeddings.Embed(path)
                    : embeddingCache.GetOrCompute(path, options.Embeddings.Embed);

                embeddings[index] = Normalize(vector, options.Embeddings.Dimensions, path);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new SimilarImageFailure(
                    path,
                    exception.Message,
                    SimilarImageFailureStage.Embedding));
            }
            finally
            {
                int done = Interlocked.Increment(ref processed);

                if (done % 100 == 0)
                {
                    lock (progressLock)
                    {
                        progress.WriteLine($"  embedded {done}/{images.Count}");
                    }
                }
            }
        });

        cancellationToken.ThrowIfCancellationRequested();

        int embedded = embeddings.Count(vector => vector is not null);
        progress.WriteLine($"Embedded {embedded}/{images.Count} image(s).");

        return embeddings;
    }

    private static List<SimilarImageGroup> BuildGroups(
        List<(DuplicateFile File, PerceptualHash Hash)> images,
        float[]?[]? embeddings,
        SimilarImageOptions options,
        ParallelOptions parallelOptions,
        ConcurrentBag<SimilarImageFailure> failures,
        TextWriter progress,
        CancellationToken cancellationToken)
    {
        int count = images.Count;

        if (count == 0)
        {
            return new List<SimilarImageGroup>();
        }

        int[] leaders = new int[count];
        bool[] semanticLinks = new bool[count];

        BkTree<HashNode> dHashTree = new((left, right) => left.Hash.DistanceTo(right.Hash));
        BkTree<SemanticNode>? semanticTree = embeddings is null
            ? null
            : new BkTree<SemanticNode>((left, right) => left.Hash.DistanceTo(right.Hash));
        List<int> embeddedImages = new();
        bool exhaustiveEmbeddings = count <= options.BruteForceEmbeddingLimit;

        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PerceptualHash hash = images[index].Hash;
            HashNode node = new(index, hash);

            HashNode? perceptual = dHashTree.FindNearest(
                node,
                options.MaxDistance,
                candidate => candidate.Hash.HasSimilarAspectRatio(hash));

            int semanticLeader = -1;

            if (embeddings is not null && embeddings[index] is not null)
            {
                float[] current = embeddings[index]!;
                double bestSimilarity = double.MinValue;
                double secondBestSimilarity = double.MinValue;
                int bestCandidate = -1;

                IEnumerable<int> candidates = exhaustiveEmbeddings
                    ? embeddedImages
                    : semanticTree!
                        .Search(
                            new SemanticNode(index, SimHash.Compute(current)),
                            options.SimHashMaxDistance)
                        .Select(candidate => candidate.Index);

                foreach (int candidate in candidates)
                {
                    float[]? candidateEmbedding = embeddings[candidate];

                    if (candidateEmbedding is null)
                    {
                        continue;
                    }

                    double similarity = Cosine(current, candidateEmbedding);

                    if (similarity > bestSimilarity)
                    {
                        secondBestSimilarity = bestSimilarity;
                        bestSimilarity = similarity;
                        bestCandidate = candidate;
                    }
                    else if (similarity > secondBestSimilarity)
                    {
                        secondBestSimilarity = similarity;
                    }
                }

                if (bestCandidate >= 0 &&
                    bestSimilarity >= options.SemanticThreshold &&
                    (bestSimilarity >= options.SemanticStrongMatch ||
                     bestSimilarity - secondBestSimilarity >= options.SemanticMargin))
                {
                    // The margin is measured against every picture, but the join
                    // still targets the leader of the candidate's group. This
                    // keeps groups star-shaped and avoids chaining.
                    int candidateLeader = leaders[bestCandidate];

                    if (embeddings[candidateLeader] is not null &&
                        Cosine(current, embeddings[candidateLeader]!) >= options.SemanticThreshold)
                    {
                        semanticLeader = candidateLeader;
                    }
                }
            }

            if (perceptual is not null)
            {
                leaders[index] = leaders[perceptual.Index];
            }
            else if (semanticLeader >= 0)
            {
                leaders[index] = semanticLeader;
                semanticLinks[index] = true;
            }
            else
            {
                leaders[index] = index;
                dHashTree.Add(node);
            }

            if (embeddings is not null && embeddings[index] is not null)
            {
                semanticTree!.Add(new SemanticNode(index, SimHash.Compute(embeddings[index]!)));
                embeddedImages.Add(index);
            }
        }

        Dictionary<int, List<int>> components = new();

        for (int index = 0; index < count; index++)
        {
            int leader = leaders[index];

            if (!components.TryGetValue(leader, out List<int>? members))
            {
                members = new List<int>();
                components[leader] = members;
            }

            members.Add(index);
        }

        double?[]? quality = ScoreGroups(
            images,
            components,
            options,
            parallelOptions,
            failures,
            progress,
            cancellationToken);

        List<SimilarImageGroup> groups = new();

        foreach ((int leaderIndex, List<int> members) in components)
        {
            if (members.Count < 2)
            {
                continue;
            }

            int keeper = ChooseKeeper(images, members, quality);
            SimilarImage leaderImage = ToSimilarImage(images[leaderIndex]);
            SimilarImage keeperImage = ToSimilarImage(images[keeper]);

            int maximumDistance = 0;
            double? minimumSimilarity = null;
            List<SimilarImageMatch> others = new(members.Count - 1);

            foreach (int index in members)
            {
                int distance = images[leaderIndex].Hash.DistanceTo(images[index].Hash);
                maximumDistance = Math.Max(maximumDistance, distance);

                double? similarity = null;

                if (embeddings is not null &&
                    embeddings[leaderIndex] is not null &&
                    embeddings[index] is not null)
                {
                    similarity = Cosine(embeddings[leaderIndex]!, embeddings[index]!);

                    if (minimumSimilarity is null || similarity < minimumSimilarity)
                    {
                        minimumSimilarity = similarity;
                    }
                }

                if (index == keeper)
                {
                    continue;
                }

                others.Add(new SimilarImageMatch(
                    ToSimilarImage(images[index]),
                    distance,
                    similarity,
                    quality?[index]));
            }

            double? keeperSimilarity = null;

            if (embeddings is not null &&
                embeddings[leaderIndex] is not null &&
                embeddings[keeper] is not null)
            {
                keeperSimilarity = Cosine(embeddings[leaderIndex]!, embeddings[keeper]!);
            }

            others.Sort((left, right) =>
            {
                int distanceComparison = left.Distance.CompareTo(right.Distance);

                return distanceComparison != 0
                    ? distanceComparison
                    : PathUtilities.PathComparer.Compare(left.Image.Path, right.Image.Path);
            });

            List<SimilarImageMatch> matches = new(members.Count)
            {
                new(
                    keeperImage,
                    images[leaderIndex].Hash.DistanceTo(images[keeper].Hash),
                    keeperSimilarity,
                    quality?[keeper])
            };

            matches.AddRange(others);

            bool hasSemanticLink = false;

            foreach (int index in members)
            {
                if (semanticLinks[index])
                {
                    hasSemanticLink = true;
                    break;
                }
            }

            groups.Add(new SimilarImageGroup(
                matches,
                maximumDistance,
                minimumSimilarity,
                hasSemanticLink ? SimilarImageKind.Semantic : SimilarImageKind.Perceptual,
                leaderImage,
                keeperImage,
                quality?[keeper]));
        }

        progress.WriteLine($"Compared {count} hash(es), {components.Count} distinct image(s).");

        return groups
            .OrderBy(group => group.Keeper.Path, PathUtilities.PathComparer)
            .ToList();
    }

    private static double?[]? ScoreGroups(
        List<(DuplicateFile File, PerceptualHash Hash)> images,
        Dictionary<int, List<int>> components,
        SimilarImageOptions options,
        ParallelOptions parallelOptions,
        ConcurrentBag<SimilarImageFailure> failures,
        TextWriter progress,
        CancellationToken cancellationToken)
    {
        if (options.Quality is null)
        {
            return null;
        }

        List<int> members = components.Values
            .Where(component => component.Count > 1)
            .SelectMany(component => component)
            .ToList();

        if (members.Count == 0)
        {
            return null;
        }

        double?[] quality = new double?[images.Count];

        progress.WriteLine($"Scoring {members.Count} image(s) with {options.Quality.Id}...");

        Parallel.ForEach(members, parallelOptions, index =>
        {
            try
            {
                quality[index] = options.Quality.Score(images[index].File.Path);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new SimilarImageFailure(
                    images[index].File.Path,
                    exception.Message,
                    SimilarImageFailureStage.Quality));
            }
        });

        cancellationToken.ThrowIfCancellationRequested();
        return quality;
    }

    private static int ChooseKeeper(
        List<(DuplicateFile File, PerceptualHash Hash)> images,
        List<int> members,
        double?[]? quality)
    {
        int largest = members[0];

        foreach (int index in members)
        {
            long pixels = PixelCount(images[index].Hash);
            long largestPixels = PixelCount(images[largest].Hash);

            if (pixels > largestPixels ||
                pixels == largestPixels &&
                PathUtilities.PathComparer.Compare(images[index].File.Path, images[largest].File.Path) < 0)
            {
                largest = index;
            }
        }

        if (quality is null)
        {
            return largest;
        }

        long maximumPixels = PixelCount(images[largest].Hash);

        // Never promote a thumbnail over a full-resolution original. Quality
        // only arbitrates between copies that are within 5% of the resolution
        // of the largest member.
        long minimumPixels = Math.Max(1, maximumPixels * 95 / 100);
        int best = -1;
        double bestScore = double.MinValue;

        foreach (int index in members)
        {
            if (PixelCount(images[index].Hash) < minimumPixels ||
                quality[index] is not double score)
            {
                continue;
            }

            if (best < 0 || score > bestScore)
            {
                best = index;
                bestScore = score;
                continue;
            }

            if (score != bestScore)
            {
                continue;
            }

            int resolutionComparison = CompareResolution(images[index].Hash, images[best].Hash);

            if (resolutionComparison > 0 ||
                resolutionComparison == 0 &&
                PathUtilities.PathComparer.Compare(images[index].File.Path, images[best].File.Path) < 0)
            {
                best = index;
            }
        }

        return best >= 0 ? best : largest;
    }

    private static long PixelCount(PerceptualHash hash)
    {
        return (long)hash.Width * hash.Height;
    }

    private static int CompareResolution(PerceptualHash left, PerceptualHash right)
    {
        return PixelCount(left).CompareTo(PixelCount(right));
    }

    private static float[] Normalize(float[] vector, int dimensions, string path)
    {
        if (vector.Length != dimensions)
        {
            throw new InvalidDataException(
                $"Expected an embedding of {dimensions} values but got {vector.Length}: {path}");
        }

        double sum = 0;

        foreach (float value in vector)
        {
            sum += (double)value * value;
        }

        if (sum <= 0)
        {
            throw new InvalidDataException($"Embedding is empty: {path}");
        }

        float scale = (float)(1.0 / Math.Sqrt(sum));
        float[] normalized = new float[dimensions];

        for (int index = 0; index < dimensions; index++)
        {
            normalized[index] = vector[index] * scale;
        }

        return normalized;
    }

    private static double Cosine(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (int index = 0; index < left.Length; index++)
        {
            dot += (double)left[index] * right[index];
            leftNorm += (double)left[index] * left[index];
            rightNorm += (double)right[index] * right[index];
        }

        if (leftNorm <= 0 || rightNorm <= 0)
        {
            return 0;
        }

        return dot / Math.Sqrt(leftNorm * rightNorm);
    }

    private static SimilarImage ToSimilarImage((DuplicateFile File, PerceptualHash Hash) image)
    {
        return new SimilarImage(image.File.Path, image.File.Length, image.Hash);
    }

    private static List<DuplicateFile> CollectCandidates(string root, SearchOption searchOption,
        FileSearchFilter filter, Func<string, bool>? pathFilter, CancellationToken cancellationToken)
    {
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = searchOption == SearchOption.AllDirectories,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchType = OperatingSystem.IsWindows() ? MatchType.Win32 : MatchType.Simple,
            ReturnSpecialDirectories = false
        };

        List<DuplicateFile> candidates = new();

        foreach (string path in Directory.EnumerateFiles(root, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pathFilter != null && !pathFilter(path))
            {
                continue;
            }

            IFileInfo fileInfo;

            try
            {
                fileInfo = CustomFileInfo.Create(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (!filter.MatchesFile(fileInfo) ||
                !filter.MatchesPath(path) ||
                !DuplicateFile.TryCreate(path, out DuplicateFile? file) ||
                file is null)
            {
                continue;
            }

            candidates.Add(file);
        }

        return candidates;
    }

    private static PerceptualHash ComputeWithCache(ScanCache cache, string path)
    {
        byte[] bytes = cache.GetOrCompute(
            PerceptualHasher.CacheAlgorithm,
            path,
            filePath => PerceptualHasher.ToCacheBytes(PerceptualHasher.Compute(filePath)));

        return PerceptualHasher.TryFromCacheBytes(bytes, out PerceptualHash hash)
            ? hash
            : PerceptualHasher.Compute(path);
    }

    private sealed record HashNode(int Index, PerceptualHash Hash);

    private sealed record SemanticNode(int Index, SimHash128 Hash);
}
