using System.Text.Json;

namespace FileCompare;

/// <summary>
/// Trash that delegates the move to the operating system (XDG trash, Windows Recycle Bin
/// or the macOS Trash) while keeping the same manifest, hashes and restore guarantees as
/// the application-owned folder.
/// </summary>
internal sealed class SystemTrash
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _manifestPath;
    private readonly List<TrashEntry> _entries;
    private readonly OperationJournal _journal;
    private readonly ISystemTrashBackend _backend;
    private readonly IReadOnlyList<string> _excludedFolders;

    public SystemTrash()
        : this(ApplicationPaths.SystemTrashDirectory, SystemTrashBackendFactory.Create())
    {
    }

    internal SystemTrash(string directory, ISystemTrashBackend backend)
    {
        Directory.CreateDirectory(directory);
        _manifestPath = Path.Combine(directory, "trash-manifest.json");
        _journal = new OperationJournal(Path.Combine(directory, "operations.jsonl"));
        _entries = LoadManifest();
        _backend = backend;
        _excludedFolders = backend.ExcludedFolders;
    }

    public string Name => _backend.Name;

    public OperationJournal Journal => _journal;

    public IReadOnlyList<string> ExcludedFolders => _excludedFolders;

    public IReadOnlyList<TrashEntry> GetEntries()
    {
        lock (_sync)
        {
            return _entries.ToList();
        }
    }

    public Task<string> DeleteAsync(string filePath, long? expectedLength, DateTime? expectedLastWriteTimeUtc,
        CancellationToken cancellationToken)
    {
        return DeleteCoreAsync(filePath, expectedLength, expectedLastWriteTimeUtc, isSymbolicLink: false,
            cancellationToken);
    }

    public Task<string> DeleteSymbolicLinkAsync(string linkPath, CancellationToken cancellationToken)
    {
        return DeleteCoreAsync(linkPath, expectedLength: null, expectedLastWriteTimeUtc: null, isSymbolicLink: true,
            cancellationToken);
    }

    private async Task<string> DeleteCoreAsync(string filePath, long? expectedLength, DateTime? expectedLastWriteTimeUtc,
        bool isSymbolicLink, CancellationToken cancellationToken)
    {
        FileInfo fileInfo = new(filePath);

        if (isSymbolicLink)
        {
            if (fileInfo.LinkTarget is null)
            {
                throw new InvalidOperationException($"Not a symbolic link: {filePath}");
            }
        }
        else if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("File to trash was not found.", filePath);
        }

        if (!isSymbolicLink)
        {
            TrashFileOperations.VerifyUnchanged(fileInfo, expectedLength, expectedLastWriteTimeUtc);
        }

        string sha256 = isSymbolicLink
            ? string.Empty
            : await TrashFileOperations.ComputeSha256Async(filePath, cancellationToken).ConfigureAwait(false);

        UnixFileMode? unixFileMode = null;

        if (!isSymbolicLink && !OperatingSystem.IsWindows())
        {
            try
            {
                unixFileMode = File.GetUnixFileMode(filePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
            }
        }

        SystemTrashMoveResult moved = await _backend.MoveToTrashAsync(filePath, cancellationToken)
            .ConfigureAwait(false);

        TrashEntry entry = new(
            moved.TrashedPath,
            Path.GetFullPath(filePath),
            isSymbolicLink ? 0 : fileInfo.Length,
            fileInfo.LastWriteTimeUtc,
            fileInfo.CreationTimeUtc,
            sha256,
            fileInfo.Attributes,
            unixFileMode,
            DateTime.UtcNow,
            isSymbolicLink)
        {
            Backend = TrashBackend.System,
            SystemMetadataPath = moved.MetadataPath
        };

        try
        {
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
        }
        catch
        {
            // The move already happened. Put the file back rather than leaving an
            // untracked entry in the OS trash.
            try
            {
                await _backend.RestoreAsync(entry, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }

            throw;
        }

        _journal.Write(isSymbolicLink ? "delete-symlink" : "delete", entry.OriginalPath, entry.TrashedPath, "ok", null);
        return moved.TrashedPath;
    }

    public async Task RestoreAsync(TrashEntry entry, CancellationToken cancellationToken = default)
    {
        TrashEntry? registered = GetEntries().FirstOrDefault(candidate =>
            PathUtilities.PathComparer.Equals(candidate.TrashedPath, entry.TrashedPath));

        if (registered is null)
        {
            throw new InvalidOperationException("This trash entry is not present in the manifest.");
        }

        if (!Exists(registered.TrashedPath))
        {
            throw new FileNotFoundException("The trashed file is missing.", registered.TrashedPath);
        }

        if (!registered.IsSymbolicLink)
        {
            string currentHash = await TrashFileOperations.ComputeSha256Async(registered.TrashedPath, cancellationToken)
                .ConfigureAwait(false);

            if (!string.Equals(currentHash, registered.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The trashed file has changed since it was deleted.");
            }
        }

        if (File.Exists(registered.OriginalPath) ||
            (Directory.Exists(registered.OriginalPath) && !registered.IsSymbolicLink))
        {
            throw new IOException($"The original path already exists: {registered.OriginalPath}");
        }

        string? originalDirectory = Path.GetDirectoryName(registered.OriginalPath);
        if (originalDirectory != null)
        {
            Directory.CreateDirectory(originalDirectory);
        }

        await _backend.RestoreAsync(registered, cancellationToken).ConfigureAwait(false);

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

    public void Discard(TrashEntry entry)
    {
        TrashEntry? registered = GetEntries().FirstOrDefault(candidate =>
            PathUtilities.PathComparer.Equals(candidate.TrashedPath, entry.TrashedPath));

        if (registered is null)
        {
            throw new InvalidOperationException("This trash entry is not present in the manifest.");
        }

        _backend.Discard(registered);

        lock (_sync)
        {
            _entries.Remove(registered);
            SaveManifestLocked();
        }

        _journal.Write("discard", registered.OriginalPath, registered.TrashedPath, "ok", null);
    }

    private static bool Exists(string path)
    {
        return File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget is not null;
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
            throw new InvalidOperationException($"The system trash manifest is corrupt: {_manifestPath}", ex);
        }
    }

    private void SaveManifestLocked()
    {
        string json = JsonSerializer.Serialize(_entries, JsonOptions);
        string temporaryPath = _manifestPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _manifestPath, overwrite: true);
    }
}
