using System.Text.Json;

namespace FileCompare;

public sealed record OperationJournalEntry(
    DateTime TimestampUtc,
    string Action,
    string SourcePath,
    string? DestinationPath,
    string Result,
    string? Error);

public sealed class OperationJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly object _sync = new();
    private readonly string _path;

    public OperationJournal(string path)
    {
        _path = PathUtilities.ExpandPath(path);
        string? directory = Path.GetDirectoryName(_path);

        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void Write(string action, string sourcePath, string? destinationPath, string result, string? error)
    {
        OperationJournalEntry entry = new(DateTime.UtcNow, action, sourcePath, destinationPath, result, error);
        string line = JsonSerializer.Serialize(entry, JsonOptions);

        lock (_sync)
        {
            try
            {
                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Journaling is best-effort and must not break the operation.
            }
        }
    }
}
