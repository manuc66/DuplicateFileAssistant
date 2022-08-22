// See https://aka.ms/new-console-template for more information


using FileCompare;

Trash trash = new Trash(@"C:\PhotoTrash");

string from = @"c:\Deletre-from-here";

string otherPlace = @"c:\If-present-here";

Dictionary<string,(HashSet<string> inA, HashSet<string> inB)> findDuplicateByHash = 
    DupProvider.FindDuplicateByHash(from, SearchOption.TopDirectoryOnly, otherPlace, SearchOption.AllDirectories, Console.Out, new CancellationToken());

int deleted = 0;
foreach ((_, (HashSet<string> inFrom, HashSet<string> inOther)) in findDuplicateByHash)
{
    foreach (string pathFrom in inFrom)
    {
        bool sameFound = false;
        string? dupPath = null;
        foreach (string pathOther in inOther)
        {
            if (pathFrom != pathOther && FastFileCompare.AreFilesEqual(new FileInfo(pathFrom), new FileInfo(pathOther)))
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