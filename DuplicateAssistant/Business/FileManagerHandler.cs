using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DuplicateAssistant.Business;

public class FileManagerHandler
{
    public async Task OpenParentFolder(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (directory == null)
        {
            throw new ArgumentException($"Could not determine the parent folder of '{path}'.", nameof(path));
        }

        await StartAndWait(CreateOpenStartInfo(directory));
    }

    public async Task OpenFile(string path)
    {
        await StartAndWait(CreateOpenStartInfo(path));
    }

    public async Task RevealFileInFolder(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ProcessStartInfo startInfo = new("explorer.exe")
            {
                Arguments = $"/select,\"{path}\""
            };

            await StartAndWait(startInfo);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            ProcessStartInfo startInfo = new("open");
            startInfo.ArgumentList.Add("-R");
            startInfo.ArgumentList.Add(path);

            await StartAndWait(startInfo);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string fileUri = new Uri(path).AbsoluteUri;
            ProcessStartInfo startInfo = new("dbus-send");
            startInfo.ArgumentList.Add("--print-reply");
            startInfo.ArgumentList.Add("--dest=org.freedesktop.FileManager1");
            startInfo.ArgumentList.Add("--type=method_call");
            startInfo.ArgumentList.Add("/org/freedesktop/FileManager1");
            startInfo.ArgumentList.Add("org.freedesktop.FileManager1.ShowItems");
            startInfo.ArgumentList.Add($"array:string:{fileUri}");
            startInfo.ArgumentList.Add(string.Empty);

            try
            {
                using Process dbusShowItemsProcess = new() { StartInfo = startInfo };
                dbusShowItemsProcess.Start();
                await dbusShowItemsProcess.WaitForExitAsync();

                if (dbusShowItemsProcess.ExitCode == 0)
                {
                    // The dbus invocation can fail for a variety of reasons:
                    // - dbus is not available
                    // - no programs implement the service,
                    // - ...
                    return;
                }
            }
            catch (Win32Exception)
            {
                // dbus-send is not installed: fall back to opening the parent folder.
            }
        }

        await OpenParentFolder(path);
    }

    private static ProcessStartInfo CreateOpenStartInfo(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProcessStartInfo(path)
            {
                UseShellExecute = true
            };
        }

        string executable = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open";
        ProcessStartInfo startInfo = new(executable);
        startInfo.ArgumentList.Add(path);
        return startInfo;
    }

    private static async Task StartAndWait(ProcessStartInfo startInfo)
    {
        using Process process = new() { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync();
    }
}
