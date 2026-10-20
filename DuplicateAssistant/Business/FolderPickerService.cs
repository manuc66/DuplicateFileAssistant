using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace DuplicateAssistant.Business;

public sealed class FolderPickerService : IFolderPicker
{
    public async Task<string?> PickFolderAsync(string? currentPath)
    {
        Window mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                            ?? throw new InvalidCastException("MainWindow not found!");

        IStorageFolder? suggestedStartLocation = !string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath)
            ? await mainWindow.StorageProvider.TryGetFolderFromPathAsync(currentPath)
            : null;

        IReadOnlyList<IStorageFolder> folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Select a folder",
                AllowMultiple = false,
                SuggestedStartLocation = suggestedStartLocation
            });

        string? selectedPath = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;

        return selectedPath != null && Directory.Exists(selectedPath) ? selectedPath : null;
    }
}
