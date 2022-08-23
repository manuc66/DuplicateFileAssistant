// See https://aka.ms/new-console-template for more information


using FileCompare;

string from = @"Folder";

foreach (string entryPath in Directory.EnumerateFileSystemEntries(from, "*", SearchOption.AllDirectories))
{
    string fileName = Path.GetFileName(entryPath);
    string targetFilePath = Path.Combine(from, fileName);

    FileAttributes entryAttribute = File.GetAttributes(entryPath);
    if (!entryAttribute.HasFlag(FileAttributes.Directory))
    {
        FileInfo targetFileInfo = new(targetFilePath);
        if (targetFileInfo.Exists)
        {
            if (!targetFilePath.Equals(entryPath, StringComparison.OrdinalIgnoreCase))
            {
                if (FileExt.FilesAreEqual(targetFileInfo, new(entryPath)))
                {
                    File.Delete(entryPath);
                    Console.WriteLine($"File deleted {entryPath}");
                }
            }
        }
        else
        {
            File.Move(entryPath, targetFilePath);
            string? currentDir = Path.GetDirectoryName(entryPath);
            if (!Directory.EnumerateFileSystemEntries(currentDir).Any())
            {
                Directory.Delete(currentDir);
            }
        }
    }
    else
    {
        if (!Directory.EnumerateFileSystemEntries(entryPath).Any())
        {
            Directory.Delete(entryPath);
        }
    }
}

Console.WriteLine("Hello, World!");