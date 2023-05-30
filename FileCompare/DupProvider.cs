using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IO;
using Directory = System.IO.Directory;
using MatchType = Microsoft.IO.MatchType;
using Path = System.IO.Path;
using SearchOption = System.IO.SearchOption;

namespace FileCompare;

public class DupProvider
{
    private static Dictionary<T, (HashSet<string> inA, HashSet<string> inB)> FindDuplicate<T>(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, Func<IFileInfo, bool> filter, Func<string, T> getHash,
        TextWriter textDisplayProgress,
        CancellationToken ct) where T : notnull
    {
        Dictionary<long, HashSet<string>> fileInA = GetElementByFileSize(a, searchOption, filter, textDisplayProgress, ct);
        if (ct.IsCancellationRequested)
        {
            return new(0);
        }

        Dictionary<long, HashSet<string>> fileInB = GetElementByFileSize(b, bSearchOption, filter, textDisplayProgress, ct);
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
                    textDisplayProgress.Write($"{(int)(i / (decimal)nbToCompare * 100)}/100 - {i}/{nbToCompare} - ");
                    T hash = getHash(path1);
                    textDisplayProgress.WriteLine(hash);
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
                    textDisplayProgress.Write($"{(int)(i / (decimal)nbToCompare * 100)}/100 - {i}/{nbToCompare} - ");
                    T hash = getHash(path2);
                    textDisplayProgress.WriteLine(hash);
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

    private static Dictionary<T, HashSet<string>> FindDuplicate<T>(string inputPath, SearchOption searchOption, Func<IFileInfo, bool> filter,
        Func<string, T> getHash, TextWriter textDisplayProgress, Action<int> updateProgress, 
        CancellationToken ct) where T : notnull
    {
        List<KeyValuePair<long, HashSet<string>>> potentialDup = GetFileGroupThatHaveSameSize<T>(inputPath, searchOption, filter, textDisplayProgress, ct, out int totalToHash);

        if (ct.IsCancellationRequested)
        {
            return new();
        }

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
                try
                {
                    T hash = getHash(path);
                    duplicates.AddOrUpdate(hash, new(StringComparer.OrdinalIgnoreCase) { path },
                        tuple => tuple.Add(path));
                    hashed.Add(path);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        if (!ct.IsCancellationRequested)
        {
            updateProgress(100);
        }

        return duplicates.Where(x => x.Value.Count > 1)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static List<KeyValuePair<long, HashSet<string>>> GetFileGroupThatHaveSameSize<T>(string inputPath, SearchOption searchOption, Func<IFileInfo, bool> filter, TextWriter textDisplayProgress, CancellationToken ct, out int totalToHash)
        where T : notnull
    {
        Dictionary<long, HashSet<string>> fileBySize =
            GetElementByFileSize(inputPath, searchOption, filter, textDisplayProgress, ct);
        
        if (ct.IsCancellationRequested)
        {
            totalToHash = 0;
            return new();
        }

        List<KeyValuePair<long, HashSet<string>>> potentialDup = fileBySize.Where(x => x.Value.Count > 1).ToList();
        
        if (ct.IsCancellationRequested)
        {
            totalToHash = 0;
            return new();
        }
        
        totalToHash = potentialDup.Aggregate(new HashSet<string>(), (set, pair) =>
        {
            foreach (string path in pair.Value)
            {
                set.Add(path);
            }

            return set;
        }).Count;
        
        return potentialDup;
    }

    public static Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> FindDuplicateByHash(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, Func<IFileInfo, bool> filter, TextWriter textDisplayProgress, Func<string, string> hash,
        CancellationToken cancellationToken)
    {
        return FindDuplicate(a, searchOption, b, bSearchOption, filter, hash,
            textDisplayProgress, cancellationToken);
    }

    public static Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> FindDuplicateByFileName(string a,
        SearchOption searchOption, string b, SearchOption bSearchOption, Func<IFileInfo, bool> filter, TextWriter textDisplayProgress,
        CancellationToken cancellationToken)
    {
        return FindDuplicate(a, searchOption, b, bSearchOption, filter,
            path => Path.GetFileName(path) ?? throw new FileNotFoundException("No file name", path)
            ,textDisplayProgress, cancellationToken);
    }

    public static Dictionary<string, HashSet<string>> FindDuplicateByHash(string path, SearchOption searchOption, Func<IFileInfo, bool> filter,
        TextWriter textDisplayProgress, Action<int> updateProgress, Func<string, string> hash, CancellationToken ct)
    {
        return FindDuplicate(path, searchOption, filter, hash, textDisplayProgress, updateProgress, ct);
    }

    public static Dictionary<string, HashSet<string>> FindDuplicateByFileName(string path, SearchOption searchOption, Func<IFileInfo, bool> filter,
        TextWriter textDisplayProgress, Action<int> updateProgress, CancellationToken cancellationToken)
    {
        Func<string, string> hash = p => Path.GetFileName(p) ?? throw new FileNotFoundException("No file name", path);
        return FindDuplicate(path, searchOption, filter, hash, textDisplayProgress, updateProgress, cancellationToken);
    }

    private static Dictionary<long, HashSet<string>> GetElementByFileSize(string a, SearchOption aSearchOption,
        Func<IFileInfo, bool> filter,TextWriter textDisplayProgress,
        CancellationToken ct)
    {
        Dictionary<long, HashSet<string>> afileWithSameSize = new();

        int itemCrawled = 0;
        Stopwatch estimateElapsedTime = Stopwatch.StartNew();
        foreach (string entryPath in EnumeratePath(a, aSearchOption, ct))
        {
            if (estimateElapsedTime.ElapsedMilliseconds > 1000)
            {
                estimateElapsedTime = Stopwatch.StartNew();
                textDisplayProgress.WriteLine($"Discovered files so far: {itemCrawled}");
            }
            
            IFileInfo fileInfo = CustomFileInfo.Create(entryPath);
            if (filter(fileInfo))
            {
                long fileSize = fileInfo.Length;

                afileWithSameSize.AddOrUpdate(fileSize, new(StringComparer.OrdinalIgnoreCase) { entryPath },
                    val => val.Add(entryPath));
            }

            itemCrawled++;
        }

        textDisplayProgress.WriteLine($"Total files to analyze: {itemCrawled}");

        return afileWithSameSize;
    }

    private static IEnumerable<string> EnumeratePath(string a, SearchOption aSearchOption, CancellationToken ct)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            
           return Microsoft.IO.Directory.EnumerateFiles(a, "*", new  Microsoft.IO.EnumerationOptions()
               {
                   RecurseSubdirectories = aSearchOption ==  SearchOption.AllDirectories,
                   IgnoreInaccessible = true,
                   MatchType = MatchType.Win32,
                   ReturnSpecialDirectories = false,
               })
               .TakeWhile(_ => !ct.IsCancellationRequested);
        }
        return Directory.EnumerateFiles(a, "*",
            aSearchOption).TakeWhile(_ => !ct.IsCancellationRequested);
    }
}