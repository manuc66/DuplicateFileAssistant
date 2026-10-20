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

public class InvalidLinkViewModel : ViewModelBase
{
    private readonly Trash _trash;
    private readonly IDeleteConfirmation _deleteConfirmation;
    private readonly IFolderPicker _folderPicker;
    private string _searchPath;
    private bool _dryRun;
    private InvalidSymbolicLink? _selectedLink;

    public InvalidLinkViewModel(Trash trash, IDeleteConfirmation deleteConfirmation, IFolderPicker folderPicker,
        string searchPath)
    {
        _trash = trash;
        _deleteConfirmation = deleteConfirmation;
        _folderPicker = folderPicker;
        _searchPath = searchPath;
        Links = new ObservableCollection<InvalidSymbolicLink>();

        SelectFolderCommand = ReactiveCommand.CreateFromTask(SelectFolderAsync);
        ScanCommand = ReactiveCommand.CreateFromTask(ScanAsync);
        StopCommand = ReactiveCommand.Create(StopOperation);
        DeleteSelectedCommand = ReactiveCommand.CreateFromTask(() => DeleteAsync(all: false));
        DeleteAllCommand = ReactiveCommand.CreateFromTask(() => DeleteAsync(all: true));
    }

    public string SearchPath
    {
        get => _searchPath;
        set => this.RaiseAndSetIfChanged(ref _searchPath, value);
    }

    public bool DryRun
    {
        get => _dryRun;
        set => this.RaiseAndSetIfChanged(ref _dryRun, value);
    }

    public InvalidSymbolicLink? SelectedLink
    {
        get => _selectedLink;
        set => this.RaiseAndSetIfChanged(ref _selectedLink, value);
    }

    public ObservableCollection<InvalidSymbolicLink> Links { get; }
    public bool HasResults => Links.Count > 0;

    public ReactiveCommand<Unit, Unit> SelectFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteSelectedCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteAllCommand { get; }

    private async Task SelectFolderAsync()
    {
        string? selectedPath = await _folderPicker.PickFolderAsync(SearchPath);

        if (selectedPath != null)
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

        if (!Directory.Exists(SearchPath))
        {
            Status = "Choose an existing folder.";
            return;
        }

        Links.Clear();
        SelectedLink = null;
        RaiseLinksChanged();

        await RunOperationAsync(async cancellationToken =>
        {
            FileSearchFilter filter = FileSearchFilter.FromText(0, null, _trash.TrashFolder);
            IReadOnlyList<InvalidSymbolicLink> links = await Task.Run(
                () => InvalidSymbolicLinkFinder.Find(SearchPath, filter, cancellationToken),
                cancellationToken);

            OnUiThread(() =>
            {
                foreach (InvalidSymbolicLink link in links)
                {
                    Links.Add(link);
                }

                Status = $"{links.Count} invalid symbolic link(s) found.";
                RaiseLinksChanged();
            });
        }, "Scanning...");
    }

    private async Task DeleteAsync(bool all)
    {
        if (IsBusy)
        {
            return;
        }

        List<InvalidSymbolicLink> targets = all
            ? Links.ToList()
            : SelectedLink is null ? new List<InvalidSymbolicLink>() : new List<InvalidSymbolicLink> { SelectedLink };

        if (targets.Count == 0)
        {
            return;
        }

        if (DryRun)
        {
            Status = $"Dry run: would move {targets.Count} link(s) to the trash.";
            return;
        }

        bool confirmed = await _deleteConfirmation.ConfirmAsync(
            SearchPath,
            $"Move {targets.Count} broken symbolic link(s) to the trash?");

        if (!confirmed)
        {
            return;
        }

        await RunOperationAsync(async cancellationToken =>
        {
            int deleted = 0;
            int failed = 0;
            List<InvalidSymbolicLink> deletedLinks = new();

            foreach (InvalidSymbolicLink link in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await _trash.DeleteSymbolicLinkAsync(link.Path, cancellationToken);
                    deletedLinks.Add(link);
                    deleted++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                }
            }

            OnUiThread(() =>
            {
                foreach (InvalidSymbolicLink link in deletedLinks)
                {
                    Links.Remove(link);
                }

                SelectedLink = null;
                Status = failed == 0
                    ? $"Moved {deleted} link(s) to the trash."
                    : $"Moved {deleted} link(s); {failed} failed.";
                RaiseLinksChanged();
            });
        }, "Moving links to the trash...");
    }

    private void RaiseLinksChanged()
    {
        this.RaisePropertyChanged(nameof(HasResults));
    }
}
