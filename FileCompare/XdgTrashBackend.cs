using System.Globalization;
using System.Runtime.Versioning;
using System.Text;

namespace FileCompare;

/// <summary>
/// Linux implementation of the freedesktop.org trash specification:
/// <c>$XDG_DATA_HOME/Trash</c> for the home filesystem, and a per-volume trash for files
/// deleted from another mount.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class XdgTrashBackend : ISystemTrashBackend
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public string Name => "XDG trash";

    public IReadOnlyList<string> ExcludedFolders
    {
        get
        {
            string homeTrash = GetHomeTrashDirectory();
            List<string> folders = new() { homeTrash };

            foreach (string topDirectory in EnumerateTopDirectories())
            {
                string shared = Path.Combine(topDirectory, ".Trash");
                string privateTrash = Path.Combine(topDirectory, $".Trash-{UnixTrashNative.UserId}");

                if (Directory.Exists(shared))
                {
                    folders.Add(shared);
                }

                if (Directory.Exists(privateTrash))
                {
                    folders.Add(privateTrash);
                }
            }

            return folders.Distinct(PathUtilities.PathComparer).ToList();
        }
    }

    public async Task<SystemTrashMoveResult> MoveToTrashAsync(string filePath, CancellationToken cancellationToken)
    {
        (string filesDirectory, string infoDirectory) = ResolveTrashDirectories(filePath);

        UnixTrashNative.EnsurePrivateDirectory(filesDirectory);
        UnixTrashNative.EnsurePrivateDirectory(infoDirectory);

        string name = GetUniqueName(filesDirectory, infoDirectory, filePath);
        string destination = Path.Combine(filesDirectory, name);
        string infoPath = Path.Combine(infoDirectory, name + ".trashinfo");
        string fullPath = Path.GetFullPath(filePath);

        File.WriteAllText(
            infoPath,
            $"[Trash Info]\nPath={EncodePath(fullPath)}\nDeletionDate={DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)}\n",
            Utf8WithoutBom);

        try
        {
            await TrashFileOperations.MoveAsync(filePath, destination, new FileInfo(filePath), cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            UnixTrashNative.TryDeleteFile(infoPath);
            throw;
        }

        return new SystemTrashMoveResult(destination, infoPath);
    }

    public async Task RestoreAsync(TrashEntry entry, CancellationToken cancellationToken)
    {
        await TrashFileOperations.MoveAsync(entry.TrashedPath, entry.OriginalPath,
            new FileInfo(entry.TrashedPath), cancellationToken).ConfigureAwait(false);

        UnixTrashNative.TryDeleteFile(entry.SystemMetadataPath);
    }

    public void Discard(TrashEntry entry)
    {
        DeletePath(entry.TrashedPath);
        UnixTrashNative.TryDeleteFile(entry.SystemMetadataPath);
    }

    private static void DeletePath(string path)
    {
        if (new FileInfo(path).LinkTarget is not null)
        {
            File.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static (string FilesDirectory, string InfoDirectory) ResolveTrashDirectories(string filePath)
    {
        string homeTrash = GetHomeTrashDirectory();
        string homeTopDirectory = GetTopDirectory(homeTrash);
        string fileTopDirectory = GetTopDirectory(filePath);

        if (string.Equals(homeTopDirectory, fileTopDirectory, PathUtilities.PathComparison))
        {
            UnixTrashNative.EnsurePrivateDirectory(homeTrash);
            return (Path.Combine(homeTrash, "files"), Path.Combine(homeTrash, "info"));
        }

        string userId = UnixTrashNative.UserId.ToString(CultureInfo.InvariantCulture);
        string sharedTrash = Path.Combine(fileTopDirectory, ".Trash");
        string sharedUserTrash = Path.Combine(sharedTrash, userId);

        if (IsUsableSharedTrash(sharedTrash) && TryEnsureDirectory(sharedUserTrash))
        {
            return (Path.Combine(sharedUserTrash, "files"), Path.Combine(sharedUserTrash, "info"));
        }

        string privateTrash = Path.Combine(fileTopDirectory, $".Trash-{userId}");
        if (TryEnsureDirectory(privateTrash))
        {
            return (Path.Combine(privateTrash, "files"), Path.Combine(privateTrash, "info"));
        }

        // Last resort: copy across filesystems into the home trash.
        return (Path.Combine(homeTrash, "files"), Path.Combine(homeTrash, "info"));
    }

    private static bool TryEnsureDirectory(string path)
    {
        try
        {
            UnixTrashNative.EnsurePrivateDirectory(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsUsableSharedTrash(string sharedTrash)
    {
        if (!Directory.Exists(sharedTrash))
        {
            return false;
        }

        try
        {
            UnixFileMode mode = File.GetUnixFileMode(sharedTrash);
            return mode.HasFlag(UnixFileMode.StickyBit);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static string GetHomeTrashDirectory()
    {
        string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

        if (string.IsNullOrWhiteSpace(dataHome) || !Path.IsPathRooted(dataHome))
        {
            dataHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share");
        }

        return Path.Combine(dataHome, "Trash");
    }

    private static string GetTopDirectory(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? best = null;

        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                string root = drive.RootDirectory.FullName;

                if (PathUtilities.IsUnder(fullPath, root) &&
                    (best is null || root.Length > best.Length))
                {
                    best = root;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return best ?? Path.GetPathRoot(fullPath) ?? Path.DirectorySeparatorChar.ToString();
    }

    private static IEnumerable<string> EnumerateTopDirectories()
    {
        List<string> roots = new();

        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                string root = drive.RootDirectory.FullName;

                if (!roots.Contains(root, PathUtilities.PathComparer))
                {
                    roots.Add(root);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return roots;
    }

    private static string GetUniqueName(string filesDirectory, string infoDirectory, string filePath)
    {
        string fileName = Path.GetFileName(filePath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "file";
        }

        string baseName =
            $"{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff", CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}-{fileName}";
        string name = baseName;
        int suffix = 1;

        while (PathExists(filesDirectory, name) || File.Exists(Path.Combine(infoDirectory, name + ".trashinfo")))
        {
            name = $"{Path.GetFileNameWithoutExtension(baseName)}-{suffix++}{Path.GetExtension(baseName)}";
        }

        return name;
    }

    private static bool PathExists(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        return File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget is not null;
    }

    private static string EncodePath(string path)
    {
        return Uri.EscapeDataString(path).Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
    }
}
