using System.Runtime.Versioning;

namespace FileCompare;

internal sealed record SystemTrashMoveResult(string TrashedPath, string? MetadataPath);

internal interface ISystemTrashBackend
{
    string Name { get; }

    /// <summary>
    /// Folders that should never be scanned, such as the OS trash directories.
    /// </summary>
    IReadOnlyList<string> ExcludedFolders { get; }

    Task<SystemTrashMoveResult> MoveToTrashAsync(string filePath, CancellationToken cancellationToken);

    Task RestoreAsync(TrashEntry entry, CancellationToken cancellationToken);

    void Discard(TrashEntry entry);
}

internal static class SystemTrashBackendFactory
{
    public static ISystemTrashBackend Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsTrashBackend();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacTrashBackend();
        }

        if (OperatingSystem.IsLinux())
        {
            return new XdgTrashBackend();
        }

        throw new PlatformNotSupportedException("The system trash is not supported on this platform.");
    }
}

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static class UnixTrashNative
{
    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getuid")]
    private static extern uint GetUidLibc();

    [System.Runtime.InteropServices.DllImport("libSystem.dylib", EntryPoint = "getuid")]
    private static extern uint GetUidLibSystem();

    public static uint UserId
    {
        get
        {
            try
            {
                return GetUidLibc();
            }
            catch (DllNotFoundException)
            {
                try
                {
                    return GetUidLibSystem();
                }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
                {
                    return 0;
                }
            }
            catch (EntryPointNotFoundException)
            {
                return 0;
            }
        }
    }

    public static void EnsurePrivateDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);

            try
            {
                File.SetUnixFileMode(path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
            }
        }
    }

    public static void TryDeleteFile(string? path)
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
}
