// See https://aka.ms/new-console-template for more information


using FileCompare;

Dictionary<string, HashSet<string>> fileWithSameName = new(StringComparer.OrdinalIgnoreCase);
Dictionary<string, int> extensionFound = new(StringComparer.OrdinalIgnoreCase);

foreach (string entryPath in Directory.EnumerateFileSystemEntries(@"C:\Folder", "*",
             SearchOption.AllDirectories))
{
    FileAttributes entryAttribute = File.GetAttributes(entryPath);
    if (entryAttribute.HasFlag(FileAttributes.Directory))
    {
        continue;
    }

    string extension = Path.GetExtension(entryPath);
    if (extensionFound.TryGetValue(extension, out int count))
    {
        extensionFound[extension] = ++count;
    }
    else
    {
        extensionFound[extension] = 1;
    }

    string fileName = Path.GetFileName(entryPath);

    if (fileWithSameName.TryGetValue(fileName, out HashSet<string> sameName))
    {
        sameName.Add(entryPath);
    }
    else
    {
        fileWithSameName[fileName] = new() { entryPath };
    }
}


Dictionary<string, int> duplicateFolderHit = new(StringComparer.OrdinalIgnoreCase);
int duplicateCase = 0;
foreach ((string fileName, HashSet<string> paths) in fileWithSameName.OrderBy(x => x.Value.Count))
{
    if (paths.Count > 1)
    {
        duplicateCase++;
        Console.WriteLine($"{fileName} has potential {paths.Count} duplicate");

        foreach (string path in paths)
        {
            String directoryPath = Path.GetDirectoryName(path);
            if (duplicateFolderHit.TryGetValue(directoryPath, out int count))
            {
                duplicateFolderHit[directoryPath] = ++count;
            }
            else
            {
                duplicateFolderHit[directoryPath] = 1;
            }
        }
    }
}


Console.WriteLine($"Directories which contains lots of duplicate:");
duplicateFolderHit.OrderBy(x => x.Value).ToList()
    .ForEach(x => Console.WriteLine($"\t{x.Key} - {x.Value}"));

int trueSimilarFount = 0;
Dictionary<string, int> folderHit = new();
string folderWithMostDuplicate = duplicateFolderHit.OrderBy(x => x.Value).Last().Key;
foreach ((string fileName, HashSet<string> paths) in fileWithSameName)
{
    string expectedPath = Path.Combine(folderWithMostDuplicate, fileName);
    bool match = paths.Any(path => path.Equals(expectedPath, StringComparison.OrdinalIgnoreCase));

    if (match)
    {
        FileInfo source = new(expectedPath);
        foreach (string path in paths)
        {
            if (!path.Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                FileInfo dest = new(path);

                //if (FileExt.FilesAreEqual(source, dest))
                if (FastFileCompare.AreFilesEqual(source, dest))
                {
                    trueSimilarFount++;
                    Console.WriteLine($"{source.FullName} is same as {dest.FullName}");
                }
                else
                {
                    //Console.WriteLine($"{source.FullName} is not the same as {dest.FullName}");
                }
            }
        }
    }
}

Console.WriteLine($@"Folder the most similar to {folderWithMostDuplicate} :");


Console.WriteLine($@"Folder the most similar to {folderWithMostDuplicate} :");
folderHit.OrderBy(x => x.Value).ToList().ForEach(x => Console.WriteLine($"\t{x.Key} - {x.Value}"));

Console.WriteLine($"There is {duplicateCase} duplicate case to investigate");
// Console.WriteLine($"Found exentions:");
// extensionFound.OrderBy(x => x.Value).ToList().ForEach(x => Console.WriteLine($"\t{x.Key} - {x.Value}"));