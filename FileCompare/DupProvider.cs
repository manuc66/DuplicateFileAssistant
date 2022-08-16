using System.Security.Cryptography;
using System.Text;

namespace FileCompare;

public class DupProvider
{
    static string Hash(HashAlgorithm hashAlgorithm, string path, TextWriter textDisplayProgress)
    {
        Console.Write($"Hashing {path}... ");
        using FileStream fs = File.OpenRead(path);
        var retVal = hashAlgorithm.ComputeHash(fs);
        StringBuilder sb = new StringBuilder();
        foreach (var val in retVal)
        {
            sb.Append(val.ToString("x2"));
        }

        var hash = sb.ToString();
        textDisplayProgress.WriteLine(hash);
        return hash;
    }

    private static Dictionary<T, (HashSet<string> inA, HashSet<string> inB)> FindDuplicate<T>(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, Func<string, T> getHash,
        CancellationToken ct) where T : notnull
    {
        Dictionary<long, HashSet<string>> fileInA = GetElementByFileSize(a, searchOption, ct);
        if (ct.IsCancellationRequested)
        {
            return new Dictionary<T, (HashSet<string> inA, HashSet<string> inB)>(0);
        }

        Dictionary<long, HashSet<string>> fileInB = GetElementByFileSize(b, bSearchOption, ct);
        if (ct.IsCancellationRequested)
        {
            return new Dictionary<T, (HashSet<string> inA, HashSet<string> inB)>(0);
        }

        HashSet<string> toHash = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var commonSize in fileInA.Keys.Intersect(fileInB.Keys))
        {
            foreach (var s in fileInA[commonSize])
            {
                toHash.Add(s);
            }

            foreach (var s in fileInB[commonSize])
            {
                toHash.Add(s);
            }
        }

        int nbToCompare = toHash.Count;
        HashSet<string> hashed = new HashSet<string>(nbToCompare, StringComparer.OrdinalIgnoreCase);
        int i = 0;
        Dictionary<T, (HashSet<string> inA, HashSet<string> inB)> duplicates =
            new Dictionary<T, (HashSet<string> inA, HashSet<string> inB)>();
        foreach ((long fileSize, HashSet<string> pathsA) in fileInA.TakeWhile(_ => !ct.IsCancellationRequested))
        {
            if (fileInB.TryGetValue(fileSize, out HashSet<string>? pathsB))
            {
                foreach (var path1 in pathsA
                             .TakeWhile(_ => !ct.IsCancellationRequested)
                             .Where(path1 => !hashed.Contains(path1)))
                {
                    i++;
                    Console.Write($"{(int)(i / (decimal)nbToCompare * 100)}/100 - {i}/{nbToCompare} - ");
                    var hash = getHash(path1);
                    Console.WriteLine(hash);
                    duplicates.AddOrUpdate(hash,
                        (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path1 },
                            new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
                        tuple => tuple.Item1.Add(path1));
                    hashed.Add(path1);
                }

                foreach (var path2 in pathsB
                             .TakeWhile(_ => !ct.IsCancellationRequested)
                             .Where(path2 => !hashed.Contains(path2)))
                {
                    i++;
                    Console.Write($"{(int)(i / (decimal)nbToCompare * 100)}/100 - {i}/{nbToCompare} - ");
                    var hash = getHash(path2);
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
        Dictionary<long, HashSet<string>> fileBySize = GetElementByFileSize(inputPath, searchOption, ct);

        if (ct.IsCancellationRequested)
        {
            return new Dictionary<T, HashSet<string>>();
        }

        var potentialDup = fileBySize.Where(x => x.Value.Count > 1).ToList();
        var totalToHash = potentialDup.Aggregate(new HashSet<string>(), (set, pair) =>
        {
            foreach (var path in pair.Value)
            {
                set.Add(path);
            }

            return set;
        }).Count;

        HashSet<string> hashed = new HashSet<string>(totalToHash, StringComparer.OrdinalIgnoreCase);
        int i = 0;
        Dictionary<T, HashSet<string>> duplicates = new Dictionary<T, HashSet<string>>(totalToHash);
        foreach ((_, HashSet<string> paths) in potentialDup
                     .TakeWhile(_ => !ct.IsCancellationRequested))
        {
            foreach (var path in paths
                         .TakeWhile(_ => !ct.IsCancellationRequested)
                         .Where(path => !hashed.Contains(path)))
            {
                i++;
                var percent = (int)(i / (decimal)totalToHash * 100);
                updateProgress(percent);
                textDisplayProgress.Write($"{percent}/100 - {i}/{totalToHash} - ");
                var hash = getHash(path);
                textDisplayProgress.WriteLine(hash);
                duplicates.AddOrUpdate(hash, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path },
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
        SearchOption searchOption, string b, SearchOption bSearchOption, TextWriter textDisplayProgress,
        CancellationToken cancellationToken)
    {
        using MD5 md5 = MD5.Create();

        return FindDuplicate(a, searchOption, b, bSearchOption, path => Hash(md5, path, textDisplayProgress),
            cancellationToken);
    }

    public static Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> FindDuplicateByFileName(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, CancellationToken cancellationToken)
    {
        return FindDuplicate(a, searchOption, b, bSearchOption,
            path => Path.GetFileName(path) ?? throw new FileNotFoundException("No file name", path), cancellationToken);
    }

    public static Dictionary<string, HashSet<string>> FindDuplicateByHash(string path, SearchOption searchOption,
        TextWriter textDisplayProgress, Action<int> updateProgress, CancellationToken ct)
    {
        using MD5 md5 = MD5.Create();

        Func<string, string> hash = p => Hash(md5, p, textDisplayProgress);
        return FindDuplicate(path, searchOption, hash, textDisplayProgress, updateProgress, ct);
    }

    public static Dictionary<string, HashSet<string>> FindDuplicateByFileName(string path, SearchOption searchOption,
        TextWriter textDisplayProgress, Action<int> updateProgress, CancellationToken cancellationToken)
    {
        Func<string, string> hash = p => Path.GetFileName(p) ?? throw new FileNotFoundException("No file name", path);
        return FindDuplicate(path, searchOption, hash, textDisplayProgress, updateProgress, cancellationToken);
    }

    private static Dictionary<long, HashSet<string>> GetElementByFileSize(string a, SearchOption aSearchOption,
        CancellationToken ct)
    {
        Dictionary<long, HashSet<string>> afileWithSameSize =
            new Dictionary<long, HashSet<string>>();

        foreach (string entryPath in Directory.EnumerateFileSystemEntries(a, "*",
                     aSearchOption).TakeWhile(_ => !ct.IsCancellationRequested))
        {
            FileAttributes entryAttribute = File.GetAttributes(entryPath);
            if (entryAttribute.HasFlag(FileAttributes.Directory))
            {
                continue;
            }

            var fileSize = new FileInfo(entryPath).Length;

            afileWithSameSize.AddOrUpdate(fileSize, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entryPath },
                val => val.Add(entryPath));
        }

        return afileWithSameSize;
    }
}