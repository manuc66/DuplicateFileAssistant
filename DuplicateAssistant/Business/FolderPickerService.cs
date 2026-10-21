using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using FileCompare;

namespace DuplicateAssistant.Business;

public sealed class FolderPickerService : IFolderPicker
{
    private readonly LastFolderStore? _lastFolderStore;

    public FolderPickerService(LastFolderStore? lastFolderStore = null)
    {
        _lastFolderStore = lastFolderStore;
    }

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

        if (selectedPath != null && Directory.Exists(selectedPath))
        {
            _lastFolderStore?.Save(selectedPath);
            return selectedPath;
        }

        return null;
    }
}
