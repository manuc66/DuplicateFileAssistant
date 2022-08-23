using System.Security.Cryptography;
using System.Text;

namespace FileCompare;

public class FindDupByHash
{
    string Hash(HashAlgorithm md6, string path, TextWriter display)
    {
        display.Write($"Hashing {path}... ");
        using FileStream fs = File.OpenRead(path);
        byte[] retVal = md6.ComputeHash(fs);
        StringBuilder sb = new();
        foreach (byte val in retVal)
        {
            sb.Append(val.ToString("x2"));
        }

        string hash = sb.ToString();
        display.WriteLine(hash);
        return hash;
    }

    public void GetDuplicate(string inputPath, TextWriter display)
    {
        Dictionary<long, HashSet<string>> fileWithSameSize = new();

        foreach (string entryPath in Directory.EnumerateFileSystemEntries(@"H:\Backup_DISK_1To\Photo", "*",
                     SearchOption.AllDirectories))
        {
            FileAttributes entryAttribute = File.GetAttributes(entryPath);
            if (entryAttribute.HasFlag(FileAttributes.Directory))
            {
                continue;
            }

            long fileSize = new FileInfo(entryPath).Length;

            if (fileWithSameSize.TryGetValue(fileSize, out HashSet<string> sameName))
            {
                sameName.Add(entryPath);
            }
            else
            {
                fileWithSameSize[fileSize] = new(StringComparer.OrdinalIgnoreCase) { entryPath };
            }
        }

        Dictionary<string, HashSet<string>> duplicateFolderWith = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> duplicateFolderHit = new(StringComparer.OrdinalIgnoreCase);
        int duplicateCase = 0;
        foreach ((long size, HashSet<string> paths) in fileWithSameSize.OrderBy(x => x.Value.Count))
        {
            if (paths.Count > 1)
            {
                duplicateCase++;
                display.WriteLine($"{size} has potential {paths.Count} duplicate");


                HashSet<string?> dupWith = paths.Select(Path.GetDirectoryName).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (string path in paths)
                {
                    String directoryPath = Path.GetDirectoryName(path);
                    if (duplicateFolderHit.TryGetValue(directoryPath, out int count))
                    {
                        duplicateFolderWith[directoryPath] = dupWith;
                        duplicateFolderHit[directoryPath] = ++count;
                    }
                    else
                    {
                        duplicateFolderHit[directoryPath] = 1;
                    }
                }
            }
        }

        List<KeyValuePair<long, HashSet<string>>> potentialDup = fileWithSameSize.Where(x => x.Value.Count > 1).ToList();
        int totalToHash = potentialDup.Aggregate(new HashSet<string>(), (set, pair) =>
        {
            foreach (string path in pair.Value)
            {
                set.Add(path);
            }

            return set;
        }).Count;

        int i = 1;
        MD5 md5 = MD5.Create();
        Dictionary<string, HashSet<string>> hashToPath = new();
        foreach ((long size, HashSet<string> paths) in potentialDup)
        {
            if (paths.Count > 1)
            {
                string md5Hash;
                foreach (string path in paths)
                {
                    md5Hash = Hash(md5, path, display);

                    display.Write($"{(int)(i / ((decimal)totalToHash) * 100)}/100 - {i}/{totalToHash} - ");

                    if (hashToPath.TryGetValue(md5Hash, out HashSet<string> sameHashPaths))
                    {
                        sameHashPaths.Add(path);
                    }
                    else
                    {
                        hashToPath[md5Hash] = new() { path };
                    }

                    i++;
                }
            }
        }
    }
}