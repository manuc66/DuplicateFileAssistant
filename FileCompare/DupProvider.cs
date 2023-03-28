using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace FileCompare;

public class DupProvider
{
    private static Dictionary<T, (HashSet<string> inA, HashSet<string> inB)> FindDuplicate<T>(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, Func<string, T> getHash,
        TextWriter textDisplayProgress,
        CancellationToken ct) where T : notnull
    {
        Dictionary<long, HashSet<string>> fileInA = GetElementByFileSize(a, searchOption, textDisplayProgress, ct);
        if (ct.IsCancellationRequested)
        {
            return new(0);
        }

        Dictionary<long, HashSet<string>> fileInB = GetElementByFileSize(b, bSearchOption, textDisplayProgress, ct);
        if (ct.IsCancellationRequested)
        {
            return new(0);
        }

        HashSet<string> toHash = new(StringComparer.OrdinalIgnoreCase);
        foreach (long commonSize in fileInA.Keys.Intersect(fileInB.Keys))
        {
            foreach (string s in fileInA[commonSize])
            {
                toHash.Add(s);
            }

            foreach (string s in fileInB[commonSize])
            {
                toHash.Add(s);
            }
        }

        int nbToCompare = toHash.Count;
        HashSet<string> hashed = new(nbToCompare, StringComparer.OrdinalIgnoreCase);
        int i = 0;
        Dictionary<T, (HashSet<string> inA, HashSet<string> inB)> duplicates = new();
        foreach ((long fileSize, HashSet<string> pathsA) in fileInA.TakeWhile(_ => !ct.IsCancellationRequested))
        {
            if (fileInB.TryGetValue(fileSize, out HashSet<string>? pathsB))
            {
                foreach (string path1 in pathsA
                             .TakeWhile(_ => !ct.IsCancellationRequested)
                             .Where(path1 => !hashed.Contains(path1)))
                {
                    i++;
                    Console.Write($"{(int)(i / (decimal)nbToCompare * 100)}/100 - {i}/{nbToCompare} - ");
                    T hash = getHash(path1);
                    Console.WriteLine(hash);
                    duplicates.AddOrUpdate(hash,
                        (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path1 },
                            new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
                        tuple => tuple.Item1.Add(path1));
                    hashed.Add(path1);
                }

                foreach (string path2 in pathsB
                             .TakeWhile(_ => !ct.IsCancellationRequested)
                             .Where(path2 => !hashed.Contains(path2)))
                {
                    i++;
                    Console.Write($"{(int)(i / (decimal)nbToCompare * 100)}/100 - {i}/{nbToCompare} - ");
                    T hash = getHash(path2);
                    Console.WriteLine(hash);
                    duplicates.AddOrUpdate(hash,
                        (new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path2 }),
                        tuple => tuple.Item2.Add(path2));
                    hashed.Add(path2);
                }
            }
        }

        return duplicates.Where(x => x.Value.inA.Count > 0 && x.Value.inB.Count > 0)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static Dictionary<T, HashSet<string>> FindDuplicate<T>(string inputPath, SearchOption searchOption,
        Func<string, T> getHash, TextWriter textDisplayProgress, Action<int> updateProgress,
        CancellationToken ct) where T : notnull
    {
        Dictionary<long, HashSet<string>> fileBySize =
            GetElementByFileSize(inputPath, searchOption, textDisplayProgress, ct);

        if (ct.IsCancellationRequested)
        {
            return new();
        }

        List<KeyValuePair<long, HashSet<string>>> potentialDup = fileBySize.Where(x => x.Value.Count > 1).ToList();
        int totalToHash = potentialDup.Aggregate(new HashSet<string>(), (set, pair) =>
        {
            foreach (string path in pair.Value)
            {
                set.Add(path);
            }

            return set;
        }).Count;

        HashSet<string> hashed = new(totalToHash, StringComparer.OrdinalIgnoreCase);
        int i = 0;
        Dictionary<T, HashSet<string>> duplicates = new(totalToHash);
        foreach ((_, HashSet<string> paths) in potentialDup
                     .TakeWhile(_ => !ct.IsCancellationRequested))
        {
            foreach (string path in paths
                         .TakeWhile(_ => !ct.IsCancellationRequested)
                         .Where(path => !hashed.Contains(path)))
            {
                i++;
                int percent = (int)(i / (decimal)totalToHash * 100);
                updateProgress(percent);
                textDisplayProgress.Write($"{percent}/100 - {i}/{totalToHash} - ");
                T hash = getHash(path);
                duplicates.AddOrUpdate(hash, new(StringComparer.OrdinalIgnoreCase) { path },
                    tuple => tuple.Add(path));
                hashed.Add(path);
            }
        }

        if (!ct.IsCancellationRequested)
        {
            updateProgress(100);
        }

        return duplicates.Where(x => x.Value.Count > 1)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public static Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> FindDuplicateByHash(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, TextWriter textDisplayProgress, Func<string, string> hash,
        CancellationToken cancellationToken)
    {
        return FindDuplicate(a, searchOption, b, bSearchOption, hash,
            textDisplayProgress, cancellationToken);
    }

    public static Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> FindDuplicateByFileName(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, TextWriter textDisplayProgress,
        CancellationToken cancellationToken)
    {
        return FindDuplicate(a, searchOption, b, bSearchOption,
            path => Path.GetFileName(path) ?? throw new FileNotFoundException("No file name", path),
            textDisplayProgress, cancellationToken);
    }

    public static Dictionary<string, HashSet<string>> FindDuplicateByHash(string path, SearchOption searchOption,
        TextWriter textDisplayProgress, Action<int> updateProgress, Func<string, string> hash, CancellationToken ct)
    {
        return FindDuplicate(path, searchOption, hash, textDisplayProgress, updateProgress, ct);
    }

    public static Dictionary<string, HashSet<string>> FindDuplicateByFileName(string path, SearchOption searchOption,
        TextWriter textDisplayProgress, Action<int> updateProgress, CancellationToken cancellationToken)
    {
        Func<string, string> hash = p => Path.GetFileName(p) ?? throw new FileNotFoundException("No file name", path);
        return FindDuplicate(path, searchOption, hash, textDisplayProgress, updateProgress, cancellationToken);
    }

    private static Dictionary<long, HashSet<string>> GetElementByFileSize(string a, SearchOption aSearchOption,
        TextWriter textDisplayProgress,
        CancellationToken ct)
    {
        Dictionary<long, HashSet<string>> afileWithSameSize = new();

        int itemCrawled = 0;
        Stopwatch estimateElapsedTime = Stopwatch.StartNew();
        foreach (string entryPath in Directory.EnumerateFiles(a, "*",
                     aSearchOption).TakeWhile(_ => !ct.IsCancellationRequested))
        {
            if (estimateElapsedTime.ElapsedMilliseconds > 1000)
            {
                estimateElapsedTime = Stopwatch.StartNew();
                textDisplayProgress.WriteLine($"Discovered files so far: {itemCrawled}");
            }

            long fileSize = new FileInfo(entryPath).Length;

            afileWithSameSize.AddOrUpdate(fileSize, new(StringComparer.OrdinalIgnoreCase) { entryPath },
                val => val.Add(entryPath));
            itemCrawled++;
        }

        textDisplayProgress.WriteLine($"Total files to analyze: {itemCrawled}");

        return afileWithSameSize;
    }
}