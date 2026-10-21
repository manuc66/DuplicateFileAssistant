namespace FileCompare.Tests;

internal sealed class TempDirectory : IDisposable
{
    public string Root { get; }

    public TempDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "DuplicateFileAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string CreateFile(string name, string content)
    {
        string filePath = Path.Combine(Root, name);
        string? directory = Path.GetDirectoryName(filePath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, content);
        return filePath;
    }

    public string CreateFile(string name, byte[] content)
    {
        string filePath = Path.Combine(Root, name);
        string? directory = Path.GetDirectoryName(filePath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(filePath, content);
        return filePath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
