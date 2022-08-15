// See https://aka.ms/new-console-template for more information

using System.Security.Cryptography;
using System.Text;
using FileCompare;


Trash trash = new Trash(@"C:\PhotoTrash");


var cleanIn = @"c:\ToClean";

HashSet<string> pathToTreat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (string entryPath in Directory.EnumerateFileSystemEntries(cleanIn, "*", SearchOption.AllDirectories))
{
    FileAttributes entryAttribute = File.GetAttributes(entryPath);
    if (entryAttribute.HasFlag(FileAttributes.Directory))
    {
        pathToTreat.Add(entryPath);
    }

}

int deleted = 0;
foreach (var path in pathToTreat)
{
    EliminateDuplicateIn(path, ref deleted);
}

Console.WriteLine($"Total deleted: {deleted}");



string Hash(HashAlgorithm md6, string path)
{
    Console.Write($"Hashing {path}... ");
    using FileStream fs = File.OpenRead(path);
    var retVal = md6.ComputeHash(fs);
    StringBuilder sb = new StringBuilder();
    foreach (var val in retVal)
    {
        sb.Append(val.ToString("x2"));
    }

    var hash = sb.ToString();
    Console.WriteLine(hash);
    return hash;
}

void EliminateDuplicateIn(string folder, ref int nbDeleted)
{
    

  
        Dictionary<long, HashSet<string>> fileWithSameSize =
            new Dictionary<long, HashSet<string>>();

        foreach (string entryPath in Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.TopDirectoryOnly))
        {
            FileAttributes entryAttribute = File.GetAttributes(entryPath);
            if (entryAttribute.HasFlag(FileAttributes.Directory))
            {
                continue;
            }

            var fileSize = new FileInfo(entryPath).Length;

            if (fileWithSameSize.TryGetValue(fileSize, out HashSet<string> sameName))
            {
                sameName.Add(entryPath);
            }
            else
            {
                fileWithSameSize[fileSize] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entryPath };
            }
        }

        var potentialDup = fileWithSameSize.Where(x => x.Value.Count > 1).ToList();
        var totalToHash = potentialDup.Aggregate(new HashSet<string>(), (set, pair) =>
        {
            foreach (var path in pair.Value)
            {
                set.Add(path);
            }

            return set;
        }).Count;

        int i = 1;
        MD5 md5 = MD5.Create();
        Dictionary<string, HashSet<string>> hashToPath = new Dictionary<string, HashSet<string>>();
        foreach ((long size, HashSet<string> paths) in potentialDup)
        {
            if (paths.Count > 1)
            {
                string md5Hash;
                foreach (var path in paths)
                {
                    md5Hash = Hash(md5, path);

                    Console.Write($"{(int)((decimal)i / ((decimal)totalToHash) * 100)}/100 - {i}/{totalToHash} - ");

                    if (hashToPath.TryGetValue(md5Hash, out HashSet<string> sameHashPaths))
                    {
                        sameHashPaths.Add(path);
                    }
                    else
                    {
                        hashToPath[md5Hash] = new HashSet<string>() { path };
                    }

                    i++;
                }
            }
        }

        foreach ((string hash, HashSet<string> paths) in hashToPath.Where(x => x.Value.Count > 1))
        {
            Console.WriteLine(hash + " -> ");

            var toKeep = paths.OrderByDescending(x => x.Length).Last();

            foreach (var path in paths)
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