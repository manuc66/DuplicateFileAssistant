// See https://aka.ms/new-console-template for more information


using System.Security.Cryptography;
using System.Text;

string Hash(HashAlgorithm md6, string path)
{
    Console.Write($"Hashing {path}... ");
    using FileStream fs = File.OpenRead(path);
    byte[] retVal = md6.ComputeHash(fs);
    StringBuilder sb = new StringBuilder();
    foreach (byte val in retVal)
    {
        sb.Append(val.ToString("x2"));
    }

    string hash = sb.ToString();
    Console.WriteLine(hash);
    return hash;
}

Dictionary<long, HashSet<string>> fileWithSameSize =
    new Dictionary<long, HashSet<string>>();

foreach (string entryPath in Directory.EnumerateFileSystemEntries(@"C:\Folder", "*",
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
        fileWithSameSize[fileSize] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entryPath };
    }
}

Dictionary<string, HashSet<string>> duplicateFolderWith = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
Dictionary<string, int> duplicateFolderHit = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
int duplicateCase = 0;
foreach ((long size, HashSet<string> paths) in fileWithSameSize.OrderBy(x => x.Value.Count))
{
    if (paths.Count > 1)
    {
        duplicateCase++;
        Console.WriteLine($"{size} has potential {paths.Count} duplicate");


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
Dictionary<string, HashSet<string>> hashToPath = new Dictionary<string, HashSet<string>>();
foreach ((long size, HashSet<string> paths) in potentialDup)
{
    if (paths.Count > 1)
    {
        string md5Hash;
        foreach (string path in paths)
        {
            md5Hash = Hash(md5, path);
            
            Console.Write($"{(int)((decimal)i/((decimal)totalToHash) * 100)}/100 - {i}/{totalToHash} - ");

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

foreach ( (string hash, HashSet<string> paths) in hashToPath)
{
    if (paths.Count > 1)
    {
        Console.WriteLine(hash + " -> ");
        foreach (string path in paths)
        {
            Console.WriteLine($"\t{path}");
        }
    }
}



// Console.WriteLine($"Directories which contains lots of duplicate:");
// duplicateFolderHit.OrderBy(x => x.Value).ToList()
//     .ForEach(x => Console.WriteLine($"\t{x.Key} - {x.Value}"));
//
// Console.WriteLine($"Directories which contains duplicate with others:");
// duplicateFolderWith.OrderBy(x => duplicateFolderHit[x.Key]).ToList()
//     .ForEach(x =>
//     {
//         Console.WriteLine($"\t{x.Key} - {duplicateFolderHit[x.Key]}");
//         foreach (var other in x.Value)
//         {
//             Console.WriteLine($"\t\t{other}");
//         }
//     });

Console.WriteLine($"Potential duplicate case: {duplicateCase}");



Console.WriteLine("Hello, World!");