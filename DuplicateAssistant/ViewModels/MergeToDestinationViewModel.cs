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

public class MergeToDestinationViewModel : ViewModelBase
{
    private readonly Trash _trash;
    private readonly IDeleteConfirmation _deleteConfirmation;
    private CancellationTokenSource? _cancellation;
    private string _sourcePath;
    private string _destinationPath;
    private string _status = "Ready.";
    private bool _isBusy;

    public MergeToDestinationViewModel(Trash trash, string sourcePath, IDeleteConfirmation deleteConfirmation)
    {
        _trash = trash;
        _sourcePath = sourcePath;
        _destinationPath = string.Empty;
        _deleteConfirmation = deleteConfirmation;
        Plan = new ObservableCollection<MergePlanItem>();

        BrowseSourceCommand = ReactiveCommand.CreateFromTask(() => BrowseAsync(path => SourcePath = path, SourcePath));
        BrowseDestinationCommand = ReactiveCommand.CreateFromTask(() => BrowseAsync(path => DestinationPath = path, DestinationPath));
        ScanCommand = ReactiveCommand.CreateFromTask(ScanAsync);
        StopCommand = ReactiveCommand.Create(Stop);
        MergeCommand = ReactiveCommand.CreateFromTask(MergeAsync);
    }

    public string SourcePath
    {
        get => _sourcePath;
        set => this.RaiseAndSetIfChanged(ref _sourcePath, value);
    }

    public string DestinationPath
    {
        get => _destinationPath;
        set => this.RaiseAndSetIfChanged(ref _destinationPath, value);
    }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public ObservableCollection<MergePlanItem> Plan { get; }

    public int MoveCount => Plan.Count(item => item.Action == MergeAction.Move);
    public int TrashCount => Plan.Count(item => item.Action == MergeAction.TrashDuplicate);
    public int ConflictCount => Plan.Count(item => item.Action == MergeAction.Conflict);
    public bool HasPlan => Plan.Count > 0;

    public string Summary => $"Move: {MoveCount}  |  Trash duplicates: {TrashCount}  |  Conflicts: {ConflictCount}";

    public ReactiveCommand<Unit, Unit> BrowseSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseDestinationCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> MergeCommand { get; }

    private async Task BrowseAsync(Action<string> setPath, string currentPath)
    {
        Window mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                            ?? throw new InvalidCastException("MainWindow not found!");

        IStorageFolder? suggestedStartLocation = Directory.Exists(currentPath)
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
            setPath(selectedPath);
        }
    }

    private async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!Directory.Exists(SourcePath) || !Directory.Exists(DestinationPath))
        {
            Status = "Choose an existing source and destination folder.";
            return;
        }

        IsBusy = true;
        Status = "Scanning...";
        Plan.Clear();
        RaisePlanChanged();
        _cancellation = new CancellationTokenSource();

        try
        {
            IReadOnlyList<MergePlanItem> plan = await Task.Run(
                () => MergePlanner.CreatePlan(SourcePath, DestinationPath, _cancellation.Token),
                _cancellation.Token);

            OnUiThread(() =>
            {
                foreach (MergePlanItem item in plan)
                {
                    Plan.Add(item);
                }

                Status = $"{plan.Count} file(s) planned.";
                RaisePlanChanged();
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

    private async Task MergeAsync()
    {
        if (IsBusy || Plan.Count == 0)
        {
            return;
        }

        bool confirmed = await _deleteConfirmation.ConfirmAsync(
            SourcePath,
            $"Move {MoveCount} file(s), trash {TrashCount} duplicate(s) and leave {ConflictCount} conflict(s)?");

        if (!confirmed)
        {
            return;
        }

        List<MergePlanItem> plan = Plan.ToList();
        IsBusy = true;
        Status = "Merging...";
        _cancellation = new CancellationTokenSource();

        try
        {
            MergeResult result = await MergeExecutor.ExecuteAsync(plan, _trash, _cancellation.Token);

            IReadOnlyList<string> emptyDirectories = await Task.Run(
                () => EmptyDirectoryService.FindEmptyDirectories(SourcePath, _cancellation.Token),
                _cancellation.Token);
            int removedDirectories = EmptyDirectoryService.DeleteEmptyDirectories(emptyDirectories, _cancellation.Token);

            OnUiThread(() =>
            {
                Plan.Clear();
                Status = $"Moved {result.Moved}, trashed {result.TrashedDuplicates}, conflicts {result.Conflicts}, " +
                         $"failed {result.Failed}, removed {removedDirectories} empty folder(s).";
                RaisePlanChanged();
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

    private void RaisePlanChanged()
    {
        this.RaisePropertyChanged(nameof(MoveCount));
        this.RaisePropertyChanged(nameof(TrashCount));
        this.RaisePropertyChanged(nameof(ConflictCount));
        this.RaisePropertyChanged(nameof(HasPlan));
        this.RaisePropertyChanged(nameof(Summary));
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
