using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FileCompare;

public readonly record struct FileIdentity(ulong Volume, ulong FileId)
{
    public static bool TryGet(string path, out FileIdentity identity)
    {
        if (OperatingSystem.IsWindows())
        {
            return TryGetWindows(path, out identity);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return TryGetUnix(path, out identity);
        }

        identity = default;
        return false;
    }

    private static bool TryGetUnix(string path, out FileIdentity identity)
    {
        if (LinuxFileInfo.Sys.LStat(path, out LinuxFileInfo.Sys.FileStatus status) == 0)
        {
            identity = new FileIdentity((ulong)status.Dev, (ulong)status.Ino);
            return true;
        }

        identity = default;
        return false;
    }

    private static bool TryGetWindows(string path, out FileIdentity identity)
    {
        try
        {
            using SafeFileHandle handle = File.OpenHandle(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (GetFileInformationByHandle(handle, out ByHandleFileInformation information))
            {
                ulong fileId = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
                identity = new FileIdentity(information.VolumeSerialNumber, fileId);
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        identity = default;
        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle fileHandle,
        out ByHandleFileInformation fileInformation);
}
