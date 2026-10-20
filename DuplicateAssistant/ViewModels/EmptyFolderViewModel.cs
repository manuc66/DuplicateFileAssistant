using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using DuplicateAssistant.Business;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class EmptyFolderViewModel : ViewModelBase
{
    private readonly IDeleteConfirmation _deleteConfirmation;
    private readonly IFolderPicker _folderPicker;
    private readonly Trash _trash;
    private string _searchPath;

    public EmptyFolderViewModel(string searchPath, IDeleteConfirmation deleteConfirmation, IFolderPicker folderPicker,
        Trash trash)
    {
        _searchPath = searchPath;
        _deleteConfirmation = deleteConfirmation;
        _folderPicker = folderPicker;
        _trash = trash;
        EmptyDirectories = new ObservableCollection<string>();

        SelectFolderCommand = ReactiveCommand.CreateFromTask(SelectFolderAsync);
        ScanCommand = ReactiveCommand.CreateFromTask(ScanAsync);
        StopCommand = ReactiveCommand.Create(StopOperation);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteAsync);
    }

    public string SearchPath
    {
        get => _searchPath;
        set => this.RaiseAndSetIfChanged(ref _searchPath, value);
    }

    public ObservableCollection<string> EmptyDirectories { get; }

    public bool HasResults => EmptyDirectories.Count > 0;

    public ReactiveCommand<Unit, Unit> SelectFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    private async Task SelectFolderAsync()
    {
        string? selectedPath = await _folderPicker.PickFolderAsync(SearchPath);

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

        EmptyDirectories.Clear();
        RaiseHasResultsChanged();

        await RunOperationAsync(async cancellationToken =>
        {
            IReadOnlyList<string> directories = await Task.Run(
                () => EmptyDirectoryService.FindEmptyDirectories(
                    SearchPath,
                    cancellationToken,
                    directory => !_trash.IsInTrash(directory)),
                cancellationToken);

            OnUiThread(() =>
            {
                foreach (string directory in directories)
                {
                    EmptyDirectories.Add(directory);
                }

                Status = $"{directories.Count} empty folder(s) found.";
                RaiseHasResultsChanged();
            });
        }, "Scanning...");
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

        await RunOperationAsync(async cancellationToken =>
        {
            int deleted = await Task.Run(
                () => EmptyDirectoryService.DeleteEmptyDirectories(
                    directories,
                    cancellationToken,
                    (directory, success) => _trash.Journal.Write(
                        "delete-empty-folder",
                        directory,
                        null,
                        success ? "ok" : "failed",
                        null)),
                cancellationToken);

            OnUiThread(() =>
            {
                EmptyDirectories.Clear();
                Status = $"Deleted {deleted} empty folder(s).";
                RaiseHasResultsChanged();
            });
        }, "Deleting...");
    }

    private void RaiseHasResultsChanged()
    {
        this.RaisePropertyChanged(nameof(HasResults));
    }
}
