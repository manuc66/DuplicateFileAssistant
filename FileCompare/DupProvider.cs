using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using Directory = System.IO.Directory;
using MatchType = System.IO.MatchType;
using Path = System.IO.Path;
using SearchOption = System.IO.SearchOption;

namespace FileCompare;

public class DupProvider
{
    private const int MaxHashDegreeOfParallelism = 8;
    private const int PartialHashChunkSize = 64 * 1024;
    private const long PartialHashMinimumFileSize = 1024 * 1024;
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static readonly StringComparer PathComparer = PathUtilities.PathComparer;

    private static Dictionary<T, (HashSet<string> inA, HashSet<string> inB)> FindDuplicate<T>(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, Func<IFileInfo, bool> filter, Func<string, T> getHash,
        TextWriter textDisplayProgress,
        CancellationToken ct) where T : notnull
    {
        Dictionary<long, List<string>> fileInA = GetElementByFileSize(a, searchOption, filter, textDisplayProgress, ct);
        if (ct.IsCancellationRequested)
        {
            return new(0);
        }

        Dictionary<long, List<string>> fileInB = GetElementByFileSize(b, bSearchOption, filter, textDisplayProgress, ct);
        if (ct.IsCancellationRequested)
        {
            return new(0);
        }

        HashSet<string> toHash = new(PathComparer);
        foreach (long commonSize in fileInA.Keys.Intersect(fileInB.Keys))
        {
            toHash.UnionWith(fileInA[commonSize]);
            toHash.UnionWith(fileInB[commonSize]);
        }

        List<string> candidates = RefineByPartialHash(toHash, textDisplayProgress, ct);
        Dictionary<string, T> hashByPath = HashPaths(candidates, getHash, textDisplayProgress, updateProgress: null, ct);

        Dictionary<T, (List<string> inA, List<string> inB)> duplicates = new();
        foreach ((long fileSize, List<string> pathsA) in fileInA)
        {
            if (!fileInB.TryGetValue(fileSize, out List<string>? pathsB))
            {
                continue;
            }

            foreach (string path1 in pathsA)
            {
                if (!hashByPath.TryGetValue(path1, out T? hash) || hash is null)
                {
                    continue;
                }

                if (duplicates.TryGetValue(hash, out (List<string> inA, List<string> inB) group))
                {
                    group.inA.Add(path1);
                }
                else
                {
                    duplicates[hash] = (new List<string>(1) { path1 }, new List<string>());
                }
            }

            foreach (string path2 in pathsB)
            {
                if (!hashByPath.TryGetValue(path2, out T? hash) || hash is null)
                {
                    continue;
                }

                if (duplicates.TryGetValue(hash, out (List<string> inA, List<string> inB) group))
                {
                    group.inB.Add(path2);
                }
                else
                {
                    duplicates[hash] = (new List<string>(), new List<string>(1) { path2 });
                }
            }
        }

        return duplicates
            .Where(x => x.Value.inA.Count > 0 && x.Value.inB.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => (
                    new HashSet<string>(kvp.Value.inA, PathComparer),
                    new HashSet<string>(kvp.Value.inB, PathComparer)));
    }

    private static Dictionary<T, HashSet<string>> FindDuplicate<T>(string inputPath, SearchOption searchOption, Func<IFileInfo, bool> filter,
        Func<string, T> getHash, TextWriter textDisplayProgress, Action<int> updateProgress,
        CancellationToken ct) where T : notnull
    {
        List<KeyValuePair<long, List<string>>> potentialDup =
            GetFileGroupThatHaveSameSize(inputPath, searchOption, filter, textDisplayProgress, ct);

        if (ct.IsCancellationRequested)
        {
            return new();
        }

        List<string> candidates = new();
        foreach ((_, List<string> paths) in potentialDup)
        {
            candidates.AddRange(paths);
        }

        candidates = RefineByPartialHash(candidates, textDisplayProgress, ct);

        Dictionary<string, T> hashByPath = HashPaths(candidates, getHash, textDisplayProgress, updateProgress, ct);
        Dictionary<T, List<string>> duplicates = new();

        foreach (string path in candidates)
        {
            if (!hashByPath.TryGetValue(path, out T? hash) || hash is null)
            {
                continue;
            }

            if (duplicates.TryGetValue(hash, out List<string>? paths))
            {
                paths.Add(path);
            }
            else
            {
                duplicates[hash] = new List<string>(1) { path };
            }
        }

        if (!ct.IsCancellationRequested)
        {
            updateProgress(100);
        }

        return duplicates
            .Where(x => x.Value.Count > 1)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => new HashSet<string>(kvp.Value, PathComparer));
    }

    private static List<KeyValuePair<long, List<string>>> GetFileGroupThatHaveSameSize(string inputPath,
        SearchOption searchOption, Func<IFileInfo, bool> filter, TextWriter textDisplayProgress, CancellationToken ct)
    {
        Dictionary<long, List<string>> fileBySize =
            GetElementByFileSize(inputPath, searchOption, filter, textDisplayProgress, ct);

        if (ct.IsCancellationRequested)
        {
            return new();
        }

        return fileBySize.Where(x => x.Value.Count > 1).ToList();
    }

    public static Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> FindDuplicateByHash(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, Func<IFileInfo, bool> filter, TextWriter textDisplayProgress, Func<string, string> hash,
        CancellationToken cancellationToken)
    {
        Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> candidates = FindDuplicate(a, searchOption, b, bSearchOption,
            filter, hash, textDisplayProgress, cancellationToken);

        return VerifyCrossContentGroups(candidates);
    }

    public static Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> FindDuplicateByFileName(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, Func<IFileInfo, bool> filter, TextWriter textDisplayProgress,
        CancellationToken cancellationToken)
    {
        return FindDuplicate(a, searchOption, b, bSearchOption, filter,
            NormalizeFileName, textDisplayProgress, cancellationToken);
    }

    public static Dictionary<string, HashSet<DuplicateFile>> FindDuplicateByHash(string path, SearchOption searchOption, Func<IFileInfo, bool> filter,
        TextWriter textDisplayProgress, Action<int> updateProgress, Func<string, string> hash, CancellationToken ct)
    {
        Dictionary<string, HashSet<string>> candidates =
            FindDuplicate(path, searchOption, filter, hash, textDisplayProgress, updateProgress, ct);

        return VerifyContentGroups(candidates);
    }

    public static Dictionary<string, HashSet<DuplicateFile>> FindDuplicateByFileName(string path, SearchOption searchOption, Func<IFileInfo, bool> filter,
        TextWriter textDisplayProgress, Action<int> updateProgress, CancellationToken cancellationToken)
    {
        Dictionary<string, HashSet<string>> candidates =
            FindDuplicate(path, searchOption, filter, NormalizeFileName, textDisplayProgress, updateProgress, cancellationToken);

        return AttachMetadata(candidates);
    }

    private static List<string> RefineByPartialHash(IReadOnlyCollection<string> paths, TextWriter textDisplayProgress,
        CancellationToken ct)
    {
        if (paths.Count < 2)
        {
            return paths.ToList();
        }

        List<string> alwaysKeep = new();
        Dictionary<(long Size, ulong Hash), List<string>> groups = new();

        foreach (string path in paths)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                long size = new FileInfo(path).Length;

                if (size <= PartialHashMinimumFileSize)
                {
                    alwaysKeep.Add(path);
                    continue;
                }

                (long Size, ulong Hash) key = (size, ComputePartialHash(path, size));

                if (groups.TryGetValue(key, out List<string>? group))
                {
                    group.Add(path);
                }
                else
                {
                    groups[key] = new List<string>(1) { path };
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                textDisplayProgress.WriteLine(ex);
            }
        }

        List<string> retained = new(alwaysKeep);
        foreach (List<string> group in groups.Values)
        {
            if (group.Count > 1)
            {
                retained.AddRange(group);
            }
        }

        return retained;
    }

    private static ulong ComputePartialHash(string path, long size)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(PartialHashChunkSize);

        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.SequentialScan);

            int read = ReadUpTo(stream, buffer.AsSpan(0, PartialHashChunkSize));
            ulong hash = ComputeFnv1a(buffer.AsSpan(0, read));

            if (size > PartialHashChunkSize)
            {
                stream.Position = Math.Max(PartialHashChunkSize, size - PartialHashChunkSize);
                int tailRead = ReadUpTo(stream, buffer.AsSpan(0, PartialHashChunkSize));
                hash = ComputeFnv1a(buffer.AsSpan(0, tailRead), hash);
            }

            return hash;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static int ReadUpTo(Stream stream, Span<byte> buffer)
    {
        int readTotal = 0;

        while (readTotal < buffer.Length)
        {
            int read = stream.Read(buffer[readTotal..]);
            if (read == 0)
            {
                break;
            }

            readTotal += read;
        }

        return readTotal;
    }

    private static ulong ComputeFnv1a(ReadOnlySpan<byte> data, ulong hash = FnvOffsetBasis)
    {
        foreach (byte value in data)
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        return hash;
    }

    private static Dictionary<string, T> HashPaths<T>(IEnumerable<string> paths, Func<string, T> getHash,
        TextWriter textDisplayProgress, Action<int>? updateProgress, CancellationToken ct) where T : notnull
    {
        List<string> pathList = paths as List<string> ?? paths.ToList();
        int total = pathList.Count;

        if (total == 0)
        {
            updateProgress?.Invoke(100);
            return new Dictionary<string, T>(PathComparer);
        }

        ConcurrentDictionary<string, T> hashes = new(PathComparer);
        object progressLock = new();
        int completed = 0;

        ParallelOptions options = new()
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, MaxHashDegreeOfParallelism)
        };

        try
        {
            Parallel.ForEach(pathList, options, path =>
            {
                try
                {
                    hashes[path] = getHash(path);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lock (progressLock)
                    {
                        textDisplayProgress.WriteLine(ex);
                    }
                }
                finally
                {
                    int done = Interlocked.Increment(ref completed);
                    int percent = (int)((long)done * 100 / total);

                    lock (progressLock)
                    {
                        updateProgress?.Invoke(percent);
                    }
                }
            });
        }
        catch (AggregateException ex) when (ex.Flatten().InnerExceptions.All(e => e is OperationCanceledException))
        {
            ct.ThrowIfCancellationRequested();
            throw;
        }

        updateProgress?.Invoke(100);
        return new Dictionary<string, T>(hashes, PathComparer);
    }

    private static Dictionary<string, HashSet<DuplicateFile>> VerifyContentGroups(
        Dictionary<string, HashSet<string>> candidates)
    {
        Dictionary<string, HashSet<DuplicateFile>> verified = new(StringComparer.Ordinal);

        foreach ((string hash, HashSet<string> paths) in candidates)
        {
            int index = 0;

            foreach (HashSet<DuplicateFile> group in PartitionByContent(paths))
            {
                string key = index == 0 ? hash : $"{hash}:{index}";
                verified[key] = group;
                index++;
            }
        }

        return verified;
    }

    private static Dictionary<string, HashSet<DuplicateFile>> AttachMetadata(
        Dictionary<string, HashSet<string>> candidates)
    {
        Dictionary<string, HashSet<DuplicateFile>> verified = new(StringComparer.Ordinal);

        foreach ((string key, HashSet<string> paths) in candidates)
        {
            HashSet<DuplicateFile> files = new(DuplicateFile.Comparer);

            foreach (string path in paths)
            {
                if (DuplicateFile.TryCreate(path, out DuplicateFile? file) && file is not null)
                {
                    files.Add(file);
                }
            }

            if (files.Count > 1)
            {
                verified[key] = files;
            }
        }

        return verified;
    }

    private static Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> VerifyCrossContentGroups(
        Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> candidates)
    {
        Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> verified = new(StringComparer.Ordinal);

        foreach ((string hash, (HashSet<string> inA, HashSet<string> inB) paths) in candidates)
        {
            List<(string Representative, List<string> InA, List<string> InB)> groups = new();

            foreach (string path in paths.inA)
            {
                AddToContentGroup(groups, path, isInA: true);
            }

            foreach (string path in paths.inB.Where(path => !paths.inA.Contains(path)))
            {
                AddToContentGroup(groups, path, isInA: false);
            }

            int index = 0;

            foreach ((_, List<string> groupInA, List<string> groupInB) in groups
                         .Where(group => group.InA.Count > 0 && group.InB.Count > 0))
            {
                string key = index == 0 ? hash : $"{hash}:{index}";
                verified[key] = (
                    new HashSet<string>(groupInA, PathComparer),
                    new HashSet<string>(groupInB, PathComparer));
                index++;
            }
        }

        return verified;
    }

    private static void AddToContentGroup(
        List<(string Representative, List<string> InA, List<string> InB)> groups,
        string path,
        bool isInA)
    {
        foreach ((string representative, List<string> inA, List<string> inB) in groups)
        {
            if (FilesAreEqual(representative, path))
            {
                (isInA ? inA : inB).Add(path);
                return;
            }
        }

        List<string> newInA = new();
        List<string> newInB = new();
        (isInA ? newInA : newInB).Add(path);
        groups.Add((path, newInA, newInB));
    }

    private static List<HashSet<DuplicateFile>> PartitionByContent(IEnumerable<string> paths)
    {
        List<(DuplicateFile Representative, List<DuplicateFile> Files)> groups = new();

        foreach (string path in paths)
        {
            if (!DuplicateFile.TryCreate(path, out DuplicateFile? file) || file is null)
            {
                continue;
            }

            bool added = false;

            foreach ((DuplicateFile representative, List<DuplicateFile> groupFiles) in groups)
            {
                if (FilesAreEqual(representative.Path, file.Path))
                {
                    groupFiles.Add(file);
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                groups.Add((file, new List<DuplicateFile>(1) { file }));
            }
        }

        return groups
            .Where(group => group.Files.Count > 1)
            .Select(group => new HashSet<DuplicateFile>(group.Files, DuplicateFile.Comparer))
            .ToList();
    }

    private static bool FilesAreEqual(string first, string second)
    {
        try
        {
            return FastFileCompare.AreFilesEqual(new FileInfo(first), new FileInfo(second));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string NormalizeFileName(string path)
    {
        string fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(fileName))
        {
            throw new FileNotFoundException("No file name", path);
        }

        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? fileName.ToUpperInvariant()
            : fileName;
    }

    private static Dictionary<long, List<string>> GetElementByFileSize(string a, SearchOption aSearchOption,
        Func<IFileInfo, bool> filter, TextWriter textDisplayProgress,
        CancellationToken ct)
    {
        Dictionary<long, List<string>> afileWithSameSize = new();

        int itemCrawled = 0;
        Stopwatch estimateElapsedTime = Stopwatch.StartNew();
        foreach (string entryPath in EnumeratePath(a, aSearchOption, ct))
        {
            if (estimateElapsedTime.ElapsedMilliseconds > 1000)
            {
                estimateElapsedTime = Stopwatch.StartNew();
                textDisplayProgress.WriteLine($"Discovered files so far: {itemCrawled}");
            }

            try
            {
                IFileInfo fileInfo = CustomFileInfo.Create(entryPath);
                if (filter(fileInfo))
                {
                    long fileSize = fileInfo.Length;

                    if (afileWithSameSize.TryGetValue(fileSize, out List<string>? paths))
                    {
                        paths.Add(entryPath);
                    }
                    else
                    {
                        afileWithSameSize[fileSize] = new List<string>(1) { entryPath };
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                textDisplayProgress.WriteLine(ex);
            }

            itemCrawled++;
        }

        textDisplayProgress.WriteLine($"Total files to analyze: {itemCrawled}");

        return afileWithSameSize;
    }

    private static IEnumerable<string> EnumeratePath(string a, SearchOption aSearchOption, CancellationToken ct)
    {
        return Directory.EnumerateFiles(a, "*", new EnumerationOptions
            {
                RecurseSubdirectories = aSearchOption == SearchOption.AllDirectories,
                IgnoreInaccessible = true,
                MatchType = OperatingSystem.IsWindows() ? MatchType.Win32 : MatchType.Simple,
                ReturnSpecialDirectories = false,
            })
            .TakeWhile(_ => !ct.IsCancellationRequested);
    }
}
