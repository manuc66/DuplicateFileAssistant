// See https://aka.ms/new-console-template for more information


using System.Security.Cryptography;
using System.Text;
using FileCompare;

Trash trash = new(@"C:\PhotoTrash");

string from = @"c:\Deletre-from-here";

string otherPlace = @"c:\If-present-here";

static string Hash(string path)
{
    using MD5 md5 = MD5.Create();
    Console.Write($"Hashing {path}... ");
    using FileStream fs = File.OpenRead(path);
    byte[] retVal = md5.ComputeHash(fs);
    StringBuilder sb = new();
    foreach (byte val in retVal)
    {
        sb.Append(val.ToString("x2"));
    }

    string hash = sb.ToString();
    Console.WriteLine(hash);
    return hash;
}
Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> findDuplicateByHash =
    DupProvider.FindDuplicateByHash(from, SearchOption.TopDirectoryOnly, otherPlace, SearchOption.AllDirectories, _ => true, Console.Out, Hash, new());

int deleted = 0;
foreach ((_, (HashSet<string> inFrom, HashSet<string> inOther)) in findDuplicateByHash)
{
    foreach (string pathFrom in inFrom)
    {
        bool sameFound = false;
        string? dupPath = null;
        foreach (string pathOther in inOther)
        {
            if (pathFrom != pathOther && FastFileCompare.AreFilesEqual(new(pathFrom), new(pathOther)))
            {
                dupPath = pathOther;
                sameFound = true;
                break;
            }
        }

        if (sameFound)
        {
            Console.WriteLine($"File deleted {pathFrom} \t\t duplicate with {dupPath}");
            trash.Delete(pathFrom);
            deleted++;
        }

    }
}
Console.WriteLine($"Total deleted: {deleted}");

Console.WriteLine("Hello, World!");