using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DuplicateAssistant.Business;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class EmptyFolderViewModel : ViewModelBase
{
    private readonly IDeleteConfirmation _deleteConfirmation;
    private CancellationTokenSource? _cancellation;
    private string _searchPath;
    private bool _isBusy;
    private string _status = "Ready.";

    public EmptyFolderViewModel(string searchPath, IDeleteConfirmation deleteConfirmation)
    {
        _searchPath = searchPath;
        _deleteConfirmation = deleteConfirmation;
        EmptyDirectories = new ObservableCollection<string>();

        SelectFolderCommand = ReactiveCommand.CreateFromTask(SelectFolderAsync);
        ScanCommand = ReactiveCommand.CreateFromTask(ScanAsync);
        StopCommand = ReactiveCommand.Create(Stop);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteAsync);
    }

    public string SearchPath
    {
        get => _searchPath;
        set => this.RaiseAndSetIfChanged(ref _searchPath, value);
    }

    public ObservableCollection<string> EmptyDirectories { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public bool HasResults => EmptyDirectories.Count > 0;

    public ReactiveCommand<Unit, Unit> SelectFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    private async Task SelectFolderAsync()
    {
        Window mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                            ?? throw new InvalidCastException("MainWindow not found!");

        IStorageFolder? suggestedStartLocation = Directory.Exists(SearchPath)
            ? await mainWindow.StorageProvider.TryGetFolderFromPathAsync(SearchPath)
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
            SearchPath = selectedPath;
        }
    }

    private async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Status = "Scanning...";
        EmptyDirectories.Clear();
        RaiseHasResultsChanged();
        _cancellation = new CancellationTokenSource();

        try
        {
            IReadOnlyList<string> directories = await Task.Run(
                () => EmptyDirectoryService.FindEmptyDirectories(SearchPath, _cancellation.Token),
                _cancellation.Token);

            OnUiThread(() =>
            {
                foreach (string directory in directories)
                {
                    EmptyDirectories.Add(directory);
                }

                Status = $"{directories.Count} empty folder(s) found.";
                RaiseHasResultsChanged();
            });
        }
        catch (OperationCanceledException)
        {
            OnUiThread(() => Status = "Stopped.");
        }
        catch (Exception ex)
        {
            OnUiThread(() => Status = ex.Message);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            OnUiThread(() => IsBusy = false);
        }
    }

    private void Stop()
    {
        _cancellation?.Cancel();
    }

    private async Task DeleteAsync()
    {
        if (IsBusy || EmptyDirectories.Count == 0)
        {
            return;
        }

        bool confirmed = await _deleteConfirmation.ConfirmAsync(
            SearchPath,
            $"Delete {EmptyDirectories.Count} empty folder(s)?");

        if (!confirmed)
        {
            return;
        }

        List<string> directories = EmptyDirectories.ToList();
        IsBusy = true;
        Status = "Deleting...";
        _cancellation = new CancellationTokenSource();

        try
        {
            int deleted = await Task.Run(
                () => EmptyDirectoryService.DeleteEmptyDirectories(directories, _cancellation.Token),
                _cancellation.Token);

            OnUiThread(() =>
            {
                EmptyDirectories.Clear();
                Status = $"Deleted {deleted} empty folder(s).";
                RaiseHasResultsChanged();
            });
        }
        catch (OperationCanceledException)
        {
            OnUiThread(() => Status = "Stopped.");
        }
        catch (Exception ex)
        {
            OnUiThread(() => Status = ex.Message);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            OnUiThread(() => IsBusy = false);
        }
    }

    private void RaiseHasResultsChanged()
    {
        this.RaisePropertyChanged(nameof(HasResults));
    }

    private static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
