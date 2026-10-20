using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using DuplicateAssistant.Business;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class TrashViewModel : ViewModelBase
{
    private readonly Trash _trash;
    private readonly IDeleteConfirmation _deleteConfirmation;
    private TrashEntry? _selectedEntry;

    public TrashViewModel(Trash trash, IDeleteConfirmation deleteConfirmation, TrashSettingsStore? settingsStore = null)
    {
        _trash = trash;
        _deleteConfirmation = deleteConfirmation;
        Entries = new ObservableCollection<TrashEntry>();

        RefreshCommand = ReactiveCommand.Create(Refresh);
        RestoreCommand = ReactiveCommand.CreateFromTask(RestoreAsync);
        DiscardCommand = ReactiveCommand.CreateFromTask(DiscardAsync);

        if (settingsStore != null)
        {
            settingsStore.Changed += (_, _) => OnUiThread(Refresh);
        }

        Refresh();
    }

    public ObservableCollection<TrashEntry> Entries { get; }

    public string TrashLocation => _trash.Mode == TrashMode.System
        ? "System trash"
        : _trash.TrashFolder;

    public TrashEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => this.RaiseAndSetIfChanged(ref _selectedEntry, value);
    }

    public bool HasEntries => Entries.Count > 0;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> RestoreCommand { get; }
    public ReactiveCommand<Unit, Unit> DiscardCommand { get; }

    private void Refresh()
    {
        Entries.Clear();

        foreach (TrashEntry entry in _trash.GetEntries().OrderByDescending(entry => entry.DeletedAtUtc))
        {
            Entries.Add(entry);
        }

        SelectedEntry = null;
        Status = $"{Entries.Count} item(s) in the trash.";
        this.RaisePropertyChanged(nameof(HasEntries));
        this.RaisePropertyChanged(nameof(TrashLocation));
    }

    private async Task RestoreAsync()
    {
        if (IsBusy || SelectedEntry is null)
        {
            return;
        }

        TrashEntry entry = SelectedEntry;
        bool confirmed = await _deleteConfirmation.ConfirmAsync(
            entry.OriginalPath,
            $"Restore this file to its original folder?\n\n{entry.OriginalPath}");

        if (!confirmed)
        {
            return;
        }

        await RunOperationAsync(async cancellationToken =>
        {
            await _trash.RestoreAsync(entry, cancellationToken);
            OnUiThread(() =>
            {
                Refresh();
                Status = $"Restored {entry.OriginalPath}.";
            });
        }, "Restoring...");
    }

    private async Task DiscardAsync()
    {
        if (IsBusy || SelectedEntry is null)
        {
            return;
        }

        TrashEntry entry = SelectedEntry;
        bool confirmed = await _deleteConfirmation.ConfirmAsync(
            entry.OriginalPath,
            $"Permanently delete this file from the trash?\n\n{entry.OriginalPath}\n\nThis cannot be undone.");

        if (!confirmed)
        {
            return;
        }

        await RunOperationAsync(async cancellationToken =>
        {
            await Task.Run(() => _trash.Discard(entry.TrashedPath), cancellationToken);
            OnUiThread(() =>
            {
                Refresh();
                Status = $"Discarded {entry.OriginalPath}.";
            });
        }, "Discarding...");
    }
}
