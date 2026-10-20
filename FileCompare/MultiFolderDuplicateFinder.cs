using System.Collections.Concurrent;

namespace FileCompare;

public static class MultiFolderDuplicateFinder
{
    public static Dictionary<HashKey, HashSet<DuplicateFile>> Find(IReadOnlyList<string> roots,
        SearchOption searchOption, FileSearchFilter filter, Func<string, byte[]> hash, TextWriter textDisplayProgress,
        Action<int> updateProgress, CancellationToken cancellationToken, Func<string, bool>? pathFilter = null)
    {
        if (roots.Count == 0)
        {
            throw new ArgumentException("At least one folder is required.", nameof(roots));
        }

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = searchOption == SearchOption.AllDirectories,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchType = OperatingSystem.IsWindows() ? MatchType.Win32 : MatchType.Simple,
            ReturnSpecialDirectories = false
        };

        HashSet<string> seenPaths = new(PathUtilities.PathComparer);
        Dictionary<long, List<DuplicateFile>> filesBySize = new();

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException($"Directory not found: {root}");
            }

            foreach (string path in Directory.EnumerateFiles(root, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!seenPaths.Add(path) ||
                    (pathFilter != null && !pathFilter(path)) ||
                    !filter.MatchesPath(path) ||
                    !DuplicateFile.TryCreate(path, out DuplicateFile? file) || file is null)
                {
                    continue;
                }

                if (filesBySize.TryGetValue(file.Length, out List<DuplicateFile>? files))
                {
                    files.Add(file);
                }
                else
                {
                    filesBySize[file.Length] = new List<DuplicateFile>(1) { file };
                }
            }
        }

        List<DuplicateFile> candidates = filesBySize
            .Where(pair => pair.Value.Count > 1)
            .SelectMany(pair => pair.Value)
            .ToList();

        if (candidates.Count == 0)
        {
            updateProgress(100);
            return new Dictionary<HashKey, HashSet<DuplicateFile>>();
        }

        ConcurrentDictionary<string, HashKey> hashes = new(PathUtilities.PathComparer);
        object logLock = new();
        int completed = 0;

        ParallelOptions parallelOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 8)
        };

        try
        {
            Parallel.ForEach(candidates, parallelOptions, file =>
            {
                try
                {
                    byte[] fileHash = hash(file.Path);
                    hashes[file.Path] = HashKey.FromOwned(fileHash);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lock (logLock)
                    {
                        textDisplayProgress.WriteLine(ex);
                    }
                }
                finally
                {
                    int done = Interlocked.Increment(ref completed);
                    int percent = (int)((long)done * 100 / candidates.Count);

                    lock (logLock)
                    {
                        updateProgress(percent);
                    }
                }
            });
        }
        catch (AggregateException ex) when (ex.Flatten().InnerExceptions.All(e => e is OperationCanceledException))
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }

        Dictionary<HashKey, List<DuplicateFile>> byHash = new();

        foreach (DuplicateFile file in candidates)
        {
            if (!hashes.TryGetValue(file.Path, out HashKey key))
            {
                continue;
            }

            if (byHash.TryGetValue(key, out List<DuplicateFile>? files))
            {
                files.Add(file);
            }
            else
            {
                byHash[key] = new List<DuplicateFile>(1) { file };
            }
        }

        Dictionary<HashKey, HashSet<DuplicateFile>> result = new();

        foreach ((HashKey hashKey, List<DuplicateFile> files) in byHash)
        {
            int index = 0;

            foreach (HashSet<DuplicateFile> group in DuplicateContentVerifier.Partition(files, cancellationToken))
            {
                result[index == 0 ? hashKey : hashKey.WithDiscriminator(index)] = group;
                index++;
            }
        }

        updateProgress(100);
        return result;
    }
}
