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

    public TrashViewModel(Trash trash, IDeleteConfirmation deleteConfirmation)
    {
        _trash = trash;
        _deleteConfirmation = deleteConfirmation;
        Entries = new ObservableCollection<TrashEntry>();

        RefreshCommand = ReactiveCommand.Create(Refresh);
        RestoreCommand = ReactiveCommand.CreateFromTask(RestoreAsync);

        Refresh();
    }

    public ObservableCollection<TrashEntry> Entries { get; }

    public TrashEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => this.RaiseAndSetIfChanged(ref _selectedEntry, value);
    }

    public bool HasEntries => Entries.Count > 0;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> RestoreCommand { get; }

    private void Refresh()
    {
        Entries.Clear();

        foreach (TrashEntry entry in _trash.GetEntries().OrderByDescending(entry => entry.DeletedAtUtc))
        {
            Entries.Add(entry);
        }

        Status = $"{Entries.Count} item(s) in the trash.";
        this.RaisePropertyChanged(nameof(HasEntries));
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
}
