// See https://aka.ms/new-console-template for more information


using FileCompare;

Trash trash = new(@"C:\PhotoTrash");

string from = @"c:\Remove-from-here";

string to = @"c:\To-Here";


foreach (string entryPath in Directory.EnumerateFileSystemEntries(from, "*", SearchOption.AllDirectories))
{
    FileAttributes entryAttribute = File.GetAttributes(entryPath);
    if (entryAttribute.HasFlag(FileAttributes.Directory))
    {
        continue;
    }

    string fileName = Path.GetFileName(entryPath);
    string targetFilePath = Path.Combine(to, entryPath.Substring(from.Length + 1));

    FileInfo targetFileInfo = new(targetFilePath);
    if (targetFileInfo.Exists)
    {
        if (FastFileCompare.AreFilesEqual(targetFileInfo, new(entryPath)))
        {
            trash.Delete(entryPath);
            Console.WriteLine($"File deleted {entryPath}");
        }
    }
    else
    {
        string? destDirectory = Path.GetDirectoryName(targetFilePath);
        if (destDirectory != null && !Directory.Exists(destDirectory))
        {
            Directory.CreateDirectory(destDirectory);
        }

        File.Move(entryPath, targetFilePath);
        Console.WriteLine($"File moved from {entryPath} to {targetFilePath}");
    }
}

Console.WriteLine("Hello, World!");