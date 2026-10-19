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

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

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

        Dictionary<string, T> hashByPath = HashPaths(toHash, getHash, textDisplayProgress, updateProgress: null, ct);

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

    public static Dictionary<string, HashSet<string>> FindDuplicateByHash(string path, SearchOption searchOption, Func<IFileInfo, bool> filter,
        TextWriter textDisplayProgress, Action<int> updateProgress, Func<string, string> hash, CancellationToken ct)
    {
        Dictionary<string, HashSet<string>> candidates =
            FindDuplicate(path, searchOption, filter, hash, textDisplayProgress, updateProgress, ct);

        return VerifyContentGroups(candidates);
    }

    public static Dictionary<string, HashSet<string>> FindDuplicateByFileName(string path, SearchOption searchOption, Func<IFileInfo, bool> filter,
        TextWriter textDisplayProgress, Action<int> updateProgress, CancellationToken cancellationToken)
    {
        return FindDuplicate(path, searchOption, filter, NormalizeFileName, textDisplayProgress, updateProgress, cancellationToken);
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

    private static Dictionary<string, HashSet<string>> VerifyContentGroups(
        Dictionary<string, HashSet<string>> candidates)
    {
        Dictionary<string, HashSet<string>> verified = new(StringComparer.Ordinal);

        foreach ((string hash, HashSet<string> paths) in candidates)
        {
            int index = 0;

            foreach (HashSet<string> group in PartitionByContent(paths).Where(group => group.Count > 1))
            {
                string key = index == 0 ? hash : $"{hash}:{index}";
                verified[key] = group;
                index++;
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

    private static List<HashSet<string>> PartitionByContent(IEnumerable<string> paths)
    {
        List<(string Representative, List<string> Paths)> groups = new();

        foreach (string path in paths)
        {
            bool added = false;

            foreach ((string representative, List<string> groupPaths) in groups)
            {
                if (FilesAreEqual(representative, path))
                {
                    groupPaths.Add(path);
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                groups.Add((path, new List<string>(1) { path }));
            }
        }

        return groups
            .Where(group => group.Paths.Count > 1)
            .Select(group => new HashSet<string>(group.Paths, PathComparer))
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
