using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FileCompare;

public static class HardLinkService
{
    public static void Create(string linkPath, string existingPath)
    {
        if (!File.Exists(existingPath))
        {
            throw new FileNotFoundException("The hard-link target does not exist.", existingPath);
        }

        if (File.Exists(linkPath) || Directory.Exists(linkPath))
        {
            throw new IOException($"The hard-link path already exists: {linkPath}");
        }

        if (OperatingSystem.IsWindows())
        {
            if (!CreateHardLink(linkPath, existingPath, IntPtr.Zero))
            {
                throw new IOException("Could not create a hard link.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            return;
        }

        if (link(existingPath, linkPath) != 0)
        {
            throw new IOException("Could not create a hard link.", new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

    [DllImport("libc", SetLastError = true)]
    private static extern int link(string oldPath, string newPath);
}
