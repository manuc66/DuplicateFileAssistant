using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace FileCompare;

/// <summary>
/// Windows implementation. Files are sent to the Recycle Bin with the shell so Explorer
/// can see and restore them too. The manifest keeps a pointer to the matching <c>$R</c>
/// entry and its <c>$I</c> metadata so the application can restore without Explorer.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsTrashBackend : ISystemTrashBackend
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;
    private const ushort FofWantNukeWarning = 0x4000;

    public string Name => "Windows Recycle Bin";

    public IReadOnlyList<string> ExcludedFolders
    {
        get
        {
            List<string> folders = new();

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                {
                    continue;
                }

                string recycleBin = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");

                if (Directory.Exists(recycleBin))
                {
                    folders.Add(recycleBin);
                }
            }

            return folders.Distinct(PathUtilities.PathComparer).ToList();
        }
    }

    public async Task<SystemTrashMoveResult> MoveToTrashAsync(string filePath, CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(filePath);
        string root = Path.GetPathRoot(fullPath)
                      ?? throw new IOException($"Cannot determine the volume of {fullPath}.");

        if (!Directory.Exists(Path.Combine(root, "$Recycle.Bin")))
        {
            throw new IOException("The Windows Recycle Bin is not available for this drive.");
        }

        DateTime startedUtc = DateTime.UtcNow;

        await RunOnStaThreadAsync(() => SendToRecycleBin(fullPath), cancellationToken).ConfigureAwait(false);

        string? trashedPath = FindTrashedPath(fullPath, startedUtc);

        if (trashedPath is null)
        {
            throw new IOException(
                "The file was sent to the Recycle Bin, but its entry could not be located. " +
                "It can still be restored from Windows Explorer.");
        }

        string infoPath = Path.Combine(
            Path.GetDirectoryName(trashedPath)!,
            "$I" + Path.GetFileName(trashedPath)[2..]);

        return new SystemTrashMoveResult(trashedPath, File.Exists(infoPath) ? infoPath : null);
    }

    public Task RestoreAsync(TrashEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(entry.TrashedPath) || new FileInfo(entry.TrashedPath).LinkTarget is not null)
        {
            File.Move(entry.TrashedPath, entry.OriginalPath);
        }
        else if (Directory.Exists(entry.TrashedPath))
        {
            Directory.Move(entry.TrashedPath, entry.OriginalPath);
        }
        else
        {
            throw new FileNotFoundException("The trashed file is missing.", entry.TrashedPath);
        }

        TryDelete(entry.SystemMetadataPath);
        return Task.CompletedTask;
    }

    public void Discard(TrashEntry entry)
    {
        DeletePath(entry.TrashedPath);
        TryDelete(entry.SystemMetadataPath);
    }

    private static void SendToRecycleBin(string fullPath)
    {
        IntPtr from = Marshal.StringToHGlobalUni(fullPath + "\0");

        try
        {
            SHFILEOPSTRUCT operation = new()
            {
                hwnd = IntPtr.Zero,
                wFunc = FoDelete,
                pFrom = from,
                pTo = IntPtr.Zero,
                fFlags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi | FofWantNukeWarning,
                fAnyOperationsAborted = false,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = IntPtr.Zero
            };

            int result = SHFileOperation(ref operation);

            if (operation.fAnyOperationsAborted)
            {
                throw new OperationCanceledException("The file was not deleted.");
            }

            if (result != 0)
            {
                throw new IOException($"Windows could not move the file to the Recycle Bin (error 0x{result:X}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(from);
        }
    }

    private static Task RunOnStaThreadAsync(Action action, CancellationToken cancellationToken)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "DuplicateAssistant system trash"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task.WaitAsync(cancellationToken);
    }

    private static string? FindTrashedPath(string originalPath, DateTime startedUtc)
    {
        string root = Path.GetPathRoot(originalPath)
                      ?? throw new IOException($"Cannot determine the volume of {originalPath}.");
        string recycleRoot = Path.Combine(root, "$Recycle.Bin");

        if (!Directory.Exists(recycleRoot))
        {
            return null;
        }

        string normalizedOriginal = NormalizePath(originalPath);
        string? bestPath = null;
        DateTime bestDeletion = DateTime.MinValue;

        foreach (string directory in EnumerateRecycleDirectories(recycleRoot))
        {
            try
            {
                foreach (string infoPath in Directory.EnumerateFiles(directory, "$I*"))
                {
                    if (!TryReadInfo(infoPath, out string original, out DateTime deletedAt))
                    {
                        continue;
                    }

                    if (deletedAt < startedUtc.AddMinutes(-2) ||
                        deletedAt < bestDeletion ||
                        !string.Equals(NormalizePath(original), normalizedOriginal, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string recycled = Path.Combine(directory, "$R" + Path.GetFileName(infoPath)[2..]);

                    if (File.Exists(recycled) || Directory.Exists(recycled) ||
                        new FileInfo(recycled).LinkTarget is not null)
                    {
                        bestPath = recycled;
                        bestDeletion = deletedAt;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return bestPath;
    }

    private static IEnumerable<string> EnumerateRecycleDirectories(string recycleRoot)
    {
        HashSet<string> directories = new(PathUtilities.PathComparer) { recycleRoot };

        try
        {
            foreach (string directory in Directory.EnumerateDirectories(recycleRoot))
            {
                directories.Add(directory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return directories;
    }

    private static bool TryReadInfo(string infoPath, out string originalPath, out DateTime deletedAtUtc)
    {
        originalPath = string.Empty;
        deletedAtUtc = DateTime.MinValue;

        try
        {
            using FileStream stream = new(infoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using BinaryReader reader = new(stream, Encoding.Unicode, leaveOpen: true);

            if (stream.Length < 24)
            {
                return false;
            }

            long version = reader.ReadInt64();
            reader.ReadInt64();
            long fileTime = reader.ReadInt64();

            try
            {
                deletedAtUtc = DateTime.FromFileTimeUtc(fileTime);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            if (version >= 2)
            {
                reader.ReadInt32();
            }

            long remaining = stream.Length - stream.Position;

            if (remaining <= 0 || remaining > int.MaxValue)
            {
                return false;
            }

            string path = Encoding.Unicode
                .GetString(reader.ReadBytes((int)remaining))
                .TrimEnd('\0');

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            originalPath = path;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string NormalizePath(string path)
    {
        string normalized = path.StartsWith(@"\\?\", StringComparison.Ordinal)
            ? path[4..]
            : path;

        try
        {
            normalized = Path.GetFullPath(normalized);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public IntPtr pFrom;
        public IntPtr pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public IntPtr lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOperation);
}
