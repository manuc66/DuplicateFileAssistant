using System.Text;

namespace FileCompare;

/// <summary>
/// Writes a file through a uniquely named temporary sibling and an atomic rename.
/// A crash or an exception in the writer never leaves a truncated destination file.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteAllText(string path, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        Write(path, stream =>
        {
            using StreamWriter writer = new(stream, Utf8WithoutBom, bufferSize: 4096, leaveOpen: true);
            writer.Write(contents);
        });
    }

    public static void WriteAllBytes(string path, byte[] contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        Write(path, stream => stream.Write(contents, 0, contents.Length));
    }

    public static void Write(string path, Action<Stream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(write);

        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);

        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temporary file is harmless.
        }
    }
}
