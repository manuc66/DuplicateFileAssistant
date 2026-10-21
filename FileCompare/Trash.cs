using System.Threading;
using System.Threading.Tasks;

namespace FileCompare;

/// <summary>
/// Facade over the application-owned trash folder and the operating system trash. The
/// current mode is read from <see cref="TrashSettings"/>, so changing the setting takes
/// effect for the next deletion without rebuilding the view models.
/// </summary>
public class Trash
{
    private readonly Func<TrashSettings> _settingsProvider;
    private readonly object _sync = new();
    private ApplicationTrash? _application;
    private string _applicationFolder;
    private SystemTrash? _system;

    public Trash(string trashFolder)
        : this(() => new TrashSettings(TrashMode.Application, PathUtilities.ExpandPath(trashFolder)))
    {
    }

    public Trash(Func<TrashSettings> settingsProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);

        _settingsProvider = settingsProvider;
        TrashSettings settings = settingsProvider();
        _applicationFolder = settings.TrashFolder;

        if (settings.Mode == TrashMode.Application)
        {
            _application = new ApplicationTrash(_applicationFolder);
        }
    }

    /// <summary>
    /// Application-owned folder, shown in the UI and excluded from scans. In system mode
    /// it is still the folder that would be used if the user switched back.
    /// </summary>
    public string TrashFolder => CurrentSettings.TrashFolder;

    public TrashMode Mode => CurrentSettings.Mode;

    public OperationJournal Journal => CurrentSettings.Mode == TrashMode.System
        ? SystemStore.Journal
        : ApplicationStore.Journal;

    /// <summary>
    /// Semicolon-separated list of every folder that scans must skip: the application
    /// trash and the OS trash directories.
    /// </summary>
    public string ScanExclusions
    {
        get
        {
            List<string> folders = new() { CurrentSettings.TrashFolder };
            SystemTrash? system = GetSystemStoreIfUsed();

            if (system != null)
            {
                folders.AddRange(system.ExcludedFolders);
            }

            return string.Join(';', folders.Distinct(PathUtilities.PathComparer));
        }
    }

    public bool IsInTrash(string path)
    {
        if (PathUtilities.IsUnder(path, CurrentSettings.TrashFolder))
        {
            return true;
        }

        SystemTrash? system = GetSystemStoreIfUsed();

        return system != null && system.ExcludedFolders.Any(folder => PathUtilities.IsUnder(path, folder));
    }

    public IReadOnlyList<TrashEntry> GetEntries()
    {
        List<TrashEntry> entries = new();
        ApplicationTrash? application = GetApplicationStoreIfUsed();

        if (application != null)
        {
            entries.AddRange(application.GetEntries());
        }

        SystemTrash? system = GetSystemStoreIfUsed();

        if (system != null)
        {
            entries.AddRange(system.GetEntries());
        }

        return entries;
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

    public async Task<string> DeleteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (CurrentSettings.Mode == TrashMode.System)
        {
            return await SystemStore.DeleteAsync(filePath, expectedLength: null, expectedLastWriteTimeUtc: null,
                cancellationToken).ConfigureAwait(false);
        }

        return await ApplicationStore.DeleteAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> DeleteAsync(string filePath, long expectedLength, DateTime expectedLastWriteTimeUtc,
        CancellationToken cancellationToken = default)
    {
        if (CurrentSettings.Mode == TrashMode.System)
        {
            return await SystemStore.DeleteAsync(filePath, expectedLength, expectedLastWriteTimeUtc, cancellationToken)
                .ConfigureAwait(false);
        }

        return await ApplicationStore.DeleteAsync(filePath, expectedLength, expectedLastWriteTimeUtc,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<string> DeleteSymbolicLinkAsync(string linkPath, CancellationToken cancellationToken = default)
    {
        return CurrentSettings.Mode == TrashMode.System
            ? SystemStore.DeleteSymbolicLinkAsync(linkPath, cancellationToken)
            : ApplicationStore.DeleteSymbolicLinkAsync(linkPath, cancellationToken);
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

        if (entry.Backend == TrashBackend.System)
        {
            SystemStore.Discard(entry);
        }
        else
        {
            ApplicationStore.Discard(trashedPath);
        }
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

    public Task RestoreAsync(TrashEntry entry, CancellationToken cancellationToken = default)
    {
        return entry.Backend == TrashBackend.System
            ? SystemStore.RestoreAsync(entry, cancellationToken)
            : ApplicationStore.RestoreAsync(entry, cancellationToken);
    }

    private TrashSettings CurrentSettings => _settingsProvider();

    private ApplicationTrash ApplicationStore
    {
        get
        {
            string folder = CurrentSettings.TrashFolder;

            lock (_sync)
            {
                if (_application == null ||
                    !string.Equals(_applicationFolder, folder, PathUtilities.PathComparison))
                {
                    _application = new ApplicationTrash(folder);
                    _applicationFolder = folder;
                }

                return _application;
            }
        }
    }

    private ApplicationTrash? GetApplicationStoreIfUsed()
    {
        if (_application != null)
        {
            return _application;
        }

        if (CurrentSettings.Mode == TrashMode.Application)
        {
            return ApplicationStore;
        }

        return Directory.Exists(CurrentSettings.TrashFolder) ? ApplicationStore : null;
    }

    private SystemTrash SystemStore =>
        TryGetSystemStore() ??
        throw new PlatformNotSupportedException("The system trash is not supported on this platform.");

    private SystemTrash? GetSystemStoreIfUsed()
    {
        return _system ?? (CurrentSettings.Mode == TrashMode.System ? TryGetSystemStore() : null);
    }

    private SystemTrash? TryGetSystemStore()
    {
        lock (_sync)
        {
            if (_system != null)
            {
                return _system;
            }

            try
            {
                _system = new SystemTrash();
            }
            catch (PlatformNotSupportedException)
            {
                return null;
            }

            return _system;
        }
    }
}
