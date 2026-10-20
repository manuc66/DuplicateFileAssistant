using System.Threading;
using System.Threading.Tasks;

namespace FileCompare;

public class Trash
{
    public string TrashFolder { get; }

    public Trash(string trashFolder)
    {
        TrashFolder = PathUtilities.ExpandPath(trashFolder);
        Directory.CreateDirectory(TrashFolder);
    }

    public string Delete(string filePath, long expectedLength, DateTime expectedLastWriteTimeUtc)
    {
        VerifyUnchanged(filePath, expectedLength, expectedLastWriteTimeUtc);
        return Delete(filePath);
    }

    private static void VerifyUnchanged(string filePath, long expectedLength, DateTime expectedLastWriteTimeUtc)
    {
        FileInfo fileInfo = new(filePath);

        if (fileInfo.Exists &&
            (fileInfo.Length != expectedLength || fileInfo.LastWriteTimeUtc != expectedLastWriteTimeUtc))
        {
            throw new InvalidOperationException($"File changed since it was analyzed: {filePath}");
        }
    }

    public async Task<string> DeleteAsync(string filePath, long expectedLength, DateTime expectedLastWriteTimeUtc,
        CancellationToken cancellationToken = default)
    {
        VerifyUnchanged(filePath, expectedLength, expectedLastWriteTimeUtc);
        return await DeleteAsync(filePath, cancellationToken);
    }

    public async Task<string> DeleteAsync(string filePath, CancellationToken cancellationToken = default)
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
            await using (FileStream source = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (FileStream target = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                             FileOptions.Asynchronous))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            File.Delete(filePath);
        }

        return destination;
    }

    public string Delete(string filePath)
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

        return destination;
    }

    public void Restore(string trashedPath, string originalPath)
    {
        string? originalDirectory = Path.GetDirectoryName(originalPath);
        if (originalDirectory != null)
        {
            Directory.CreateDirectory(originalDirectory);
        }

        File.Move(trashedPath, originalPath);
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
