using System.Globalization;
using System.Runtime.Versioning;

namespace FileCompare;

/// <summary>
/// macOS implementation. Files on the startup volume go to <c>~/.Trash</c>; files on
/// another mounted volume go to <c>/Volumes/&lt;volume&gt;/.Trashes/&lt;uid&gt;</c>, which is
/// where Finder looks for them.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacTrashBackend : ISystemTrashBackend
{
    public string Name => "macOS Trash";

    public IReadOnlyList<string> ExcludedFolders
    {
        get
        {
            List<string> folders = new() { GetHomeTrashDirectory() };
            string? volumesRoot = GetVolumesRoot();

            if (volumesRoot != null && Directory.Exists(volumesRoot))
            {
                try
                {
                    foreach (string volume in Directory.EnumerateDirectories(volumesRoot))
                    {
                        string trashes = Path.Combine(volume, ".Trashes");

                        if (Directory.Exists(trashes))
                        {
                            folders.Add(trashes);
                        }

                        string trash = Path.Combine(trashes,
                            UnixTrashNative.UserId.ToString(CultureInfo.InvariantCulture));

                        if (Directory.Exists(trash))
                        {
                            folders.Add(trash);
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            return folders.Distinct(PathUtilities.PathComparer).ToList();
        }
    }

    public async Task<SystemTrashMoveResult> MoveToTrashAsync(string filePath, CancellationToken cancellationToken)
    {
        string trashDirectory = ResolveTrashDirectory(filePath);
        UnixTrashNative.EnsurePrivateDirectory(trashDirectory);

        string name = GetUniqueName(trashDirectory, filePath);
        string destination = Path.Combine(trashDirectory, name);

        await TrashFileOperations.MoveAsync(filePath, destination, new FileInfo(filePath), cancellationToken)
            .ConfigureAwait(false);

        return new SystemTrashMoveResult(destination, MetadataPath: null);
    }

    public async Task RestoreAsync(TrashEntry entry, CancellationToken cancellationToken)
    {
        await TrashFileOperations.MoveAsync(entry.TrashedPath, entry.OriginalPath,
            new FileInfo(entry.TrashedPath), cancellationToken).ConfigureAwait(false);
    }

    public void Discard(TrashEntry entry)
    {
        DeletePath(entry.TrashedPath);
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

    private static string ResolveTrashDirectory(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        string? volumeRoot = GetVolumeRoot(fullPath);

        if (volumeRoot != null)
        {
            string perVolume = Path.Combine(
                volumeRoot,
                ".Trashes",
                UnixTrashNative.UserId.ToString(CultureInfo.InvariantCulture));

            if (TryEnsureDirectory(perVolume))
            {
                return perVolume;
            }
        }

        string homeTrash = GetHomeTrashDirectory();
        UnixTrashNative.EnsurePrivateDirectory(homeTrash);
        return homeTrash;
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

    private static string? GetVolumeRoot(string fullPath)
    {
        string? volumesRoot = GetVolumesRoot();

        if (volumesRoot == null || !PathUtilities.IsUnder(fullPath, volumesRoot))
        {
            return null;
        }

        string relative = Path.GetRelativePath(volumesRoot, fullPath);
        string firstPart = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(firstPart))
        {
            return null;
        }

        string volume = Path.Combine(volumesRoot, firstPart);
        return Directory.Exists(volume) ? volume : null;
    }

    private static string? GetVolumesRoot()
    {
        string root = Path.Combine(Path.DirectorySeparatorChar.ToString(), "Volumes");
        return Directory.Exists(root) ? root : null;
    }

    private static string GetHomeTrashDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".Trash");
    }

    private static string GetUniqueName(string trashDirectory, string filePath)
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

        while (PathExists(trashDirectory, name))
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
}
