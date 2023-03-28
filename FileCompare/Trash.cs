namespace FileCompare;

public class Trash
{
    public String TrashFolder;

    public Trash(string trashFolder)
    {
        TrashFolder = trashFolder;
    }

    public void Delete(string filePath)
    {
        File.Move(filePath, Path.Combine(TrashFolder, $"{DateTime.Now:yyyy-dd-M--HH-mm-ss-fff}-{Path.GetFileName(filePath)}"));
    }
}