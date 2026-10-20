namespace FileCompare;

public static class FileMetadata
{
    public static void Copy(FileInfo source, string destinationPath)
    {
        try
        {
            File.SetLastWriteTimeUtc(destinationPath, source.LastWriteTimeUtc);
            File.SetCreationTimeUtc(destinationPath, source.CreationTimeUtc);
            File.SetAttributes(destinationPath, source.Attributes);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(destinationPath, File.GetUnixFileMode(source.FullName));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Metadata is best-effort; the file content is already copied.
        }
    }

    public static void Apply(TrashEntry entry, string destinationPath)
    {
        try
        {
            File.SetLastWriteTimeUtc(destinationPath, entry.LastWriteTimeUtc);
            File.SetCreationTimeUtc(destinationPath, entry.CreationTimeUtc);
            File.SetAttributes(destinationPath, entry.Attributes);

            if (!OperatingSystem.IsWindows() && entry.UnixFileMode.HasValue)
            {
                File.SetUnixFileMode(destinationPath, entry.UnixFileMode.Value);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }
}
