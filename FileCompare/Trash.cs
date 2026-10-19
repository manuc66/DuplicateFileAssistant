namespace FileCompare;

public class Trash
{
    public string TrashFolder { get; }

    public Trash(string trashFolder)
    {
        TrashFolder = PathUtilities.ExpandPath(trashFolder);
        Directory.CreateDirectory(TrashFolder);
    }

    public void Delete(string filePath)
    {
        Directory.CreateDirectory(TrashFolder);

        string fileName = $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}-{Path.GetFileName(filePath)}";
        string destination = GetAvailableDestination(fileName);

        try
        {
            File.Move(filePath, destination);
        }
        catch (IOException) when (File.Exists(filePath) && !File.Exists(destination))
        {
            // File.Move cannot rename across volumes. Fall back to copy + delete.
            File.Copy(filePath, destination);
            File.Delete(filePath);
        }
    }

    private string GetAvailableDestination(string fileName)
    {
        string destination = Path.Combine(TrashFolder, fileName);
        int suffix = 1;

        while (File.Exists(destination) || Directory.Exists(destination))
        {
            destination = Path.Combine(
                TrashFolder,
                $"{Path.GetFileNameWithoutExtension(fileName)}-{suffix++}{Path.GetExtension(fileName)}");
        }

        return destination;
    }
}
