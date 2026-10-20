using System.Security.Cryptography;

namespace FileCompare;

internal static class TrashFileOperations
{
    /// <summary>
    /// True when any filesystem entry exists at the path, including a symbolic link
    /// whose target is missing.
    /// </summary>
    public static bool EntryExists(string path)
    {
        try
        {
            return File.Exists(path) ||
                   Directory.Exists(path) ||
                   new FileInfo(path).LinkTarget is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ArgumentException)
        {
            return false;
        }
    }

    public static void VerifyUnchanged(FileInfo fileInfo, long? expectedLength, DateTime? expectedLastWriteTimeUtc)
    {
        if (expectedLength.HasValue && fileInfo.Length != expectedLength.Value)
        {
            throw new InvalidOperationException($"File changed since it was analyzed: {fileInfo.FullName}");
        }

        if (expectedLastWriteTimeUtc.HasValue && fileInfo.LastWriteTimeUtc != expectedLastWriteTimeUtc.Value)
        {
            throw new InvalidOperationException($"File changed since it was analyzed: {fileInfo.FullName}");
        }
    }

    public static string GetAvailableDestination(string folder, string fileName)
    {
        string destination = Path.Combine(folder, fileName);
        int suffix = 1;

        while (File.Exists(destination) || Directory.Exists(destination))
        {
            destination = Path.Combine(
                folder,
                $"{Path.GetFileNameWithoutExtension(fileName)}-{suffix++}{Path.GetExtension(fileName)}");
        }

        return destination;
    }

    public static async Task MoveAsync(string sourcePath, string destinationPath, FileInfo sourceInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            File.Move(sourcePath, destinationPath);
        }
        catch (IOException) when (File.Exists(sourcePath) && !File.Exists(destinationPath))
        {
            try
            {
                await using (FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                                 FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write,
                                 FileShare.None, 81920, FileOptions.Asynchronous))
                {
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }

                FileMetadata.Copy(sourceInfo, destinationPath);
                File.Delete(sourcePath);
            }
            catch
            {
                // Never leave a partial copy behind: it would look like a valid
                // destination on the next run and block the real move.
                TryDelete(destinationPath);
                throw;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (EntryExists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }
}
