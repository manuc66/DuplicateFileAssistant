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

public class MergeToDestinationViewModel : ViewModelBase
{
    private readonly Trash _trash;
    private readonly IDeleteConfirmation _deleteConfirmation;
    private readonly IFolderPicker _folderPicker;
    private string _sourcePath;
    private string _destinationPath;
    private bool _dryRun;

    public MergeToDestinationViewModel(Trash trash, string sourcePath, IDeleteConfirmation deleteConfirmation,
        IFolderPicker folderPicker)
    {
        _trash = trash;
        _sourcePath = sourcePath;
        _destinationPath = string.Empty;
        _deleteConfirmation = deleteConfirmation;
        _folderPicker = folderPicker;
        Plan = new ObservableCollection<MergePlanItem>();

        BrowseSourceCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            string? selectedPath = await _folderPicker.PickFolderAsync(SourcePath);
            if (selectedPath != null)
            {
                SourcePath = selectedPath;
            }
        });
        BrowseDestinationCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            string? selectedPath = await _folderPicker.PickFolderAsync(DestinationPath);
            if (selectedPath != null)
            {
                DestinationPath = selectedPath;
            }
        });
        ScanCommand = ReactiveCommand.CreateFromTask(ScanAsync);
        StopCommand = ReactiveCommand.Create(StopOperation);
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

    public bool DryRun
    {
        get => _dryRun;
        set => this.RaiseAndSetIfChanged(ref _dryRun, value);
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

        Plan.Clear();
        RaisePlanChanged();

        await RunOperationAsync(async cancellationToken =>
        {
            IReadOnlyList<MergePlanItem> plan = await Task.Run(
                () => MergePlanner.CreatePlan(
                    SourcePath,
                    DestinationPath,
                    cancellationToken,
                    path => !PathUtilities.IsUnder(path, _trash.TrashFolder)),
                cancellationToken);

            OnUiThread(() =>
            {
                foreach (MergePlanItem item in plan)
                {
                    Plan.Add(item);
                }

                Status = $"{plan.Count} file(s) planned.";
                RaisePlanChanged();
            });
        }, "Scanning...");
    }

    private async Task MergeAsync()
    {
        if (IsBusy || Plan.Count == 0)
        {
            return;
        }

        if (DryRun)
        {
            Status = $"Dry run: would move {MoveCount}, trash {TrashCount} duplicate(s) and leave {ConflictCount} conflict(s).";
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

        await RunOperationAsync(async cancellationToken =>
        {
            MergeResult result = await MergeExecutor.ExecuteAsync(plan, _trash, cancellationToken, _trash.Journal);

            IReadOnlyList<string> emptyDirectories = await Task.Run(
                () => EmptyDirectoryService.FindEmptyDirectories(SourcePath, cancellationToken),
                cancellationToken);
            int removedDirectories = EmptyDirectoryService.DeleteEmptyDirectories(emptyDirectories, cancellationToken);

            OnUiThread(() =>
            {
                Plan.Clear();
                Status = $"Moved {result.Moved}, trashed {result.TrashedDuplicates}, conflicts {result.Conflicts}, " +
                         $"failed {result.Failed}, removed {removedDirectories} empty folder(s).";
                RaisePlanChanged();
            });
        }, "Merging...");
    }

    private void RaisePlanChanged()
    {
        this.RaisePropertyChanged(nameof(MoveCount));
        this.RaisePropertyChanged(nameof(TrashCount));
        this.RaisePropertyChanged(nameof(ConflictCount));
        this.RaisePropertyChanged(nameof(HasPlan));
        this.RaisePropertyChanged(nameof(Summary));
    }
}
