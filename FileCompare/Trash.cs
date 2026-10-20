using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FileCompare;

public class Trash
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _manifestPath;
    private readonly List<TrashEntry> _entries;
    private readonly OperationJournal _journal;

    public Trash(string trashFolder)
    {
        TrashFolder = PathUtilities.ExpandPath(trashFolder);
        Directory.CreateDirectory(TrashFolder);
        _manifestPath = Path.Combine(TrashFolder, "trash-manifest.json");
        _entries = LoadManifest();
        _journal = new OperationJournal(Path.Combine(TrashFolder, "operations.jsonl"));
    }

    public string TrashFolder { get; }
    public OperationJournal Journal => _journal;

    public IReadOnlyList<TrashEntry> GetEntries()
    {
        lock (_sync)
        {
            return _entries.ToList();
        }
    }

    public string Delete(string filePath)
    {
        return DeleteAsync(filePath, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public string Delete(string filePath, long expectedLength, DateTime expectedLastWriteTimeUtc)
    {
        return DeleteAsync(filePath, expectedLength, expectedLastWriteTimeUtc, CancellationToken.None)
            .ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public Task<string> DeleteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return DeleteCoreAsync(filePath, expectedLength: null, expectedLastWriteTimeUtc: null, cancellationToken);
    }

    public async Task<string> DeleteAsync(string filePath, long expectedLength, DateTime expectedLastWriteTimeUtc,
        CancellationToken cancellationToken = default)
    {
        return await DeleteCoreAsync(filePath, expectedLength, expectedLastWriteTimeUtc, cancellationToken);
    }

    private async Task<string> DeleteCoreAsync(string filePath, long? expectedLength, DateTime? expectedLastWriteTimeUtc,
        CancellationToken cancellationToken)
    {
        FileInfo fileInfo = new(filePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("File to trash was not found.", filePath);
        }

        VerifyUnchanged(fileInfo, expectedLength, expectedLastWriteTimeUtc);

        string sha256 = await ComputeSha256Async(filePath, cancellationToken).ConfigureAwait(false);
        string destination = GetAvailableDestination(
            $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}-{Guid.NewGuid():N}-{Path.GetFileName(filePath)}");

        UnixFileMode? unixFileMode = null;

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                unixFileMode = File.GetUnixFileMode(filePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
            }
        }

        TrashEntry entry = new(
            destination,
            Path.GetFullPath(filePath),
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc,
            fileInfo.CreationTimeUtc,
            sha256,
            fileInfo.Attributes,
            unixFileMode,
            DateTime.UtcNow,
            IsSymbolicLink: false);

        // Register the entry before moving the file. If the process crashes after the move,
        // the restore mapping is already persisted.
        lock (_sync)
        {
            _entries.Add(entry);

            try
            {
                SaveManifestLocked();
            }
            catch
            {
                _entries.Remove(entry);
                throw;
            }
        }

        try
        {
            await MoveFileAsync(filePath, destination, fileInfo, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_sync)
            {
                _entries.Remove(entry);

                try
                {
                    SaveManifestLocked();
                }
                catch
                {
                }
            }

            throw;
        }

        _journal.Write("delete", entry.OriginalPath, entry.TrashedPath, "ok", null);
        return destination;
    }

    public void Restore(string trashedPath, string originalPath)
    {
        RestoreAsync(trashedPath, originalPath, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public void Discard(string trashedPath)
    {
        TrashEntry? entry = GetEntries().FirstOrDefault(candidate =>
            PathUtilities.PathComparer.Equals(candidate.TrashedPath, trashedPath));

        if (entry is null)
        {
            throw new InvalidOperationException("This trash entry is not present in the manifest.");
        }

        if (File.Exists(entry.TrashedPath))
        {
            File.Delete(entry.TrashedPath);
        }

        lock (_sync)
        {
            _entries.Remove(entry);
            SaveManifestLocked();
        }

        _journal.Write("discard", entry.OriginalPath, entry.TrashedPath, "ok", null);
    }

    public async Task<string> DeleteSymbolicLinkAsync(string linkPath, CancellationToken cancellationToken = default)
    {
        FileInfo linkInfo = new(linkPath);
        string? target = linkInfo.LinkTarget;

        if (target is null)
        {
            throw new InvalidOperationException($"Not a symbolic link: {linkPath}");
        }

        string destination = GetAvailableDestination(
            $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss-fff}-{Guid.NewGuid():N}-{Path.GetFileName(linkPath)}");

        TrashEntry entry = new(
            destination,
            Path.GetFullPath(linkPath),
            0,
            linkInfo.LastWriteTimeUtc,
            linkInfo.CreationTimeUtc,
            string.Empty,
            linkInfo.Attributes,
            null,
            DateTime.UtcNow,
            IsSymbolicLink: true);

        lock (_sync)
        {
            _entries.Add(entry);

            try
            {
                SaveManifestLocked();
            }
            catch
            {
                _entries.Remove(entry);
                throw;
            }
        }

        try
        {
            await MoveFileAsync(linkPath, destination, linkInfo, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_sync)
            {
                _entries.Remove(entry);

                try
                {
                    SaveManifestLocked();
                }
                catch
                {
                }
            }

            throw;
        }

        _journal.Write("delete-symlink", entry.OriginalPath, entry.TrashedPath, "ok", null);
        return destination;
    }

    public async Task RestoreAsync(string trashedPath, string originalPath, CancellationToken cancellationToken = default)
    {
        TrashEntry? entry = GetEntries().FirstOrDefault(candidate =>
            PathUtilities.PathComparer.Equals(candidate.TrashedPath, trashedPath));

        if (entry is null)
        {
            File.Move(trashedPath, originalPath);
            return;
        }

        await RestoreAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreAsync(TrashEntry entry, CancellationToken cancellationToken = default)
    {
        TrashEntry? registered = GetEntries().FirstOrDefault(candidate =>
            PathUtilities.PathComparer.Equals(candidate.TrashedPath, entry.TrashedPath));

        if (registered is null)
        {
            throw new InvalidOperationException("This trash entry is not present in the manifest.");
        }

        if (!File.Exists(registered.TrashedPath))
        {
            throw new FileNotFoundException("The trashed file is missing.", registered.TrashedPath);
        }

        if (!registered.IsSymbolicLink)
        {
            string currentHash = await ComputeSha256Async(registered.TrashedPath, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(currentHash, registered.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The trashed file has changed since it was deleted.");
            }
        }

        if (File.Exists(registered.OriginalPath))
        {
            throw new IOException($"The original path already exists: {registered.OriginalPath}");
        }

        string? originalDirectory = Path.GetDirectoryName(registered.OriginalPath);
        if (originalDirectory != null)
        {
            Directory.CreateDirectory(originalDirectory);
        }

        await MoveFileAsync(registered.TrashedPath, registered.OriginalPath, new FileInfo(registered.TrashedPath),
            cancellationToken).ConfigureAwait(false);

        if (!registered.IsSymbolicLink)
        {
            FileMetadata.Apply(registered, registered.OriginalPath);
        }

        lock (_sync)
        {
            _entries.Remove(registered);
            SaveManifestLocked();
        }

        _journal.Write("restore", registered.OriginalPath, registered.TrashedPath, "ok", null);
    }

    private List<TrashEntry> LoadManifest()
    {
        if (!File.Exists(_manifestPath))
        {
            return new List<TrashEntry>();
        }

        try
        {
            string json = File.ReadAllText(_manifestPath);
            return JsonSerializer.Deserialize<List<TrashEntry>>(json, JsonOptions) ?? new List<TrashEntry>();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"The trash manifest is corrupt: {_manifestPath}", ex);
        }
    }

    private void SaveManifestLocked()
    {
        string json = JsonSerializer.Serialize(_entries, JsonOptions);
        string temporaryPath = _manifestPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _manifestPath, overwrite: true);
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

    private static void VerifyUnchanged(FileInfo fileInfo, long? expectedLength, DateTime? expectedLastWriteTimeUtc)
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

    private static async Task MoveFileAsync(string sourcePath, string destinationPath, FileInfo sourceInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            File.Move(sourcePath, destinationPath);
        }
        catch (IOException) when (File.Exists(sourcePath) && !File.Exists(destinationPath))
        {
            await using (FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             81920, FileOptions.Asynchronous))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            FileMetadata.Copy(sourceInfo, destinationPath);
            File.Delete(sourcePath);
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }
}
