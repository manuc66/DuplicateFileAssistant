using System.Runtime.InteropServices;

namespace FileCompare;

public class LinuxFileInfo : IFileInfo
{
    private readonly int _mode;

    public LinuxFileInfo(string filePath)
    {
        Sys.LStat(filePath, out Sys.FileStatus fileStatus);
        _mode = fileStatus.Mode;
        Length = fileStatus.Size;
    }

    public long Length { get; }

    public bool IsLink()
    {
        return (_mode & Sys.FileTypes.S_IFMT) == Sys.FileTypes.S_IFLNK;
    }

    public bool IsFifoFile()
    {
        return (_mode & Sys.FileTypes.S_IFMT) == Sys.FileTypes.S_IFIFO;
    }

    internal static class Sys
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct FileStatus
        {
            internal FileStatusFlags Flags;
            internal int Mode;
            internal uint Uid;
            internal uint Gid;
            internal long Size;
            internal long ATime;
            internal long ATimeNsec;
            internal long MTime;
            internal long MTimeNsec;
            internal long CTime;
            internal long CTimeNsec;
            internal long BirthTime;
            internal long BirthTimeNsec;
            internal long Dev;
            internal long Ino;
            internal uint UserFlags;
        }

        internal static class FileTypes
        {
            internal const int S_IFMT = 0xF000;
            internal const int S_IFIFO = 0x1000;
            internal const int S_IFCHR = 0x2000;
            internal const int S_IFDIR = 0x4000;
            internal const int S_IFREG = 0x8000;
            internal const int S_IFLNK = 0xA000;
            internal const int S_IFSOCK = 0xC000;
        }

        [Flags]
        internal enum FileStatusFlags
        {
            None = 0,
            HasBirthTime = 1,
        }

        [DllImport("libSystem.Native", EntryPoint = "SystemNative_FStat", SetLastError = true)]
        internal static extern int FStat(SafeHandle fd, out FileStatus output);

        [DllImport("libSystem.Native", EntryPoint = "SystemNative_Stat", SetLastError = true)]
        internal static extern int Stat(string path, out FileStatus output);

        [DllImport("libSystem.Native", EntryPoint = "SystemNative_LStat", SetLastError = true)]
        internal static extern int LStat(string path, out FileStatus output);
    }
}