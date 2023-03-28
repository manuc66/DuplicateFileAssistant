// See https://aka.ms/new-console-template for more information

using System.Security.Cryptography;
using System.Text;
using FileCompare;


Trash trash = new(@"C:\PhotoTrash");


string cleanIn = @"c:\ToClean";

HashSet<string> pathToTreat = new(StringComparer.OrdinalIgnoreCase);

foreach (string entryPath in Directory.EnumerateFileSystemEntries(cleanIn, "*", SearchOption.AllDirectories))
{
    FileAttributes entryAttribute = File.GetAttributes(entryPath);
    if (entryAttribute.HasFlag(FileAttributes.Directory))
    {
        pathToTreat.Add(entryPath);
    }

}

int deleted = 0;
foreach (string path in pathToTreat)
{
    EliminateDuplicateIn(path, ref deleted);
}

Console.WriteLine($"Total deleted: {deleted}");



string Hash(HashAlgorithm md6, string path)
{
    Console.Write($"Hashing {path}... ");
    using FileStream fs = File.OpenRead(path);
    byte[] retVal = md6.ComputeHash(fs);
    StringBuilder sb = new();
    foreach (byte val in retVal)
    {
        sb.Append(val.ToString("x2"));
    }

    string hash = sb.ToString();
    Console.WriteLine(hash);
    return hash;
}

void EliminateDuplicateIn(string folder, ref int nbDeleted)
{



    Dictionary<long, HashSet<string>> fileWithSameSize = new();

    foreach (string entryPath in Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.TopDirectoryOnly))
    {
        FileAttributes entryAttribute = File.GetAttributes(entryPath);
        if (entryAttribute.HasFlag(FileAttributes.Directory))
        {
            continue;
        }

        long fileSize = new FileInfo(entryPath).Length;

        if (fileWithSameSize.TryGetValue(fileSize, out HashSet<string>? sameName))
        {
            sameName.Add(entryPath);
        }
        else
        {
            fileWithSameSize[fileSize] = new(StringComparer.OrdinalIgnoreCase) { entryPath };
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
                md5Hash = Hash(md5, path);

                Console.Write($"{(int)((decimal)i / ((decimal)totalToHash) * 100)}/100 - {i}/{totalToHash} - ");

                if (hashToPath.TryGetValue(md5Hash, out HashSet<string>? sameHashPaths))
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

    foreach ((string hash, HashSet<string> paths) in hashToPath.Where(x => x.Value.Count > 1))
    {
        Console.WriteLine(hash + " -> ");

        string toKeep = paths.OrderByDescending(x => x.Length).Last();

        foreach (string path in paths)
        {
            if (path != toKeep)
            {
                trash.Delete(path);
                nbDeleted++;
            }
            // Console.WriteLine($"\t{path}");
        }

        Console.WriteLine($"\t->{toKeep}");
    }

}


Console.WriteLine("Hello, World!");