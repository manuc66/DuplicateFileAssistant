using System.Runtime.InteropServices;

namespace FileCompare;

public class CustomFileInfo
{
    public static IFileInfo Create(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxFileInfo(path);
        }
        return new CommonFileInfo(path);
    }
}