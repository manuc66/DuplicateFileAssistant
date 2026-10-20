using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using DuplicateAssistant.Business;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public abstract class DuplicateInFolderViewModel : ViewModelBase, IHaveSearchLog
{
    private readonly Subject<Unit> _stopSearch = new();
    private readonly BehaviorSubject<bool> _isSearching = new(false);
    private bool _isDeletingNonKeepers;
    private readonly IDeleteConfirmation _deleteConfirmation;
    private readonly IFolderPicker _folderPicker;
    private readonly Trash _trash;
    private readonly FileManagerHandler _fileManagerHandler;

    public ReactiveCommand<Unit, string> SelectFolderCommand { get; }
    public ReactiveCommand<Unit, Dictionary<HashKey, HashSet<DuplicateFile>>> SearchCommand { get; }
    public ReactiveCommand<Unit, Unit> StopSearchCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyRuleCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteNonKeepersCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteSelectedNonKeepersCommand { get; }

    public IReadOnlyList<string> HashAlgorithms { get; }
    public IReadOnlyList<AutoDeleteRule> AutoDeleteRules { get; } = Enum.GetValues<AutoDeleteRule>();
    public bool ShowHashOptions { get; protected set; } = true;

    private AutoDeleteRule _autoDeleteRule = AutoDeleteRule.Manual;

    public AutoDeleteRule AutoDeleteRule
    {
        get => _autoDeleteRule;
        set => this.RaiseAndSetIfChanged(ref _autoDeleteRule, value);
    }

    private bool _dryRun;

    public bool DryRun
    {
        get => _dryRun;
        set => this.RaiseAndSetIfChanged(ref _dryRun, value);
    }

    private string _hashAlgorithm = "Blake3";

    public string HashAlgorithm
    {
        get => _hashAlgorithm;
        set => this.RaiseAndSetIfChanged(ref _hashAlgorithm, value);
    }

    private string _searchPath;

    public string SearchPath
    {
        get => _searchPath;
        set => this.RaiseAndSetIfChanged(ref _searchPath, value);
    }

    private ObservableCollection<DuplicateCaseViewModel> _duplicateCaseItems;

    public ObservableCollection<DuplicateCaseViewModel> DuplicateCaseItems
    {
        get => _duplicateCaseItems;
        set => this.RaiseAndSetIfChanged(ref _duplicateCaseItems, value);
    }

    private DuplicateCaseViewModel? _duplicateCase;

    public DuplicateCaseViewModel? DuplicateCase
    {
        get => _duplicateCase;
        set => this.RaiseAndSetIfChanged(ref _duplicateCase, value);
    }

    private bool _subFolder;

    public bool SubFolder
    {
        get => _subFolder;
        set => this.RaiseAndSetIfChanged(ref _subFolder, value);
    }

    private bool _finished;

    public bool Finished
    {
        get => _finished;
        set => this.RaiseAndSetIfChanged(ref _finished, value);
    }

    private string _searchLog;

    public string SearchLog
    {
        get => _searchLog;
        set => this.RaiseAndSetIfChanged(ref _searchLog, value);
    }

    private string _minimumFileSizeText = string.Empty;

    public string MinimumFileSizeText
    {
        get => _minimumFileSizeText;
        set => this.RaiseAndSetIfChanged(ref _minimumFileSizeText, value);
    }

    private string _fileExtensions = string.Empty;

    public string FileExtensions
    {
        get => _fileExtensions;
        set => this.RaiseAndSetIfChanged(ref _fileExtensions, value);
    }

    private string _excludedFolders = string.Empty;

    public string ExcludedFolders
    {
        get => _excludedFolders;
        set => this.RaiseAndSetIfChanged(ref _excludedFolders, value);
    }

    protected DuplicateInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler,
        IDeleteConfirmation deleteConfirmation, IFolderPicker folderPicker)
    {
        _deleteConfirmation = deleteConfirmation;
        _folderPicker = folderPicker;
        _trash = trash;
        _fileManagerHandler = fileManagerHandler;
        _searchPath = searchPath;
        _searchLog = string.Empty;
        HashAlgorithms = Business.FileHashing.HashAlgorithmFactory.Names;
        SubFolder = true;
        Finished = true;
        SearchCommand = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(SearchDuplicate)
                .TakeUntil(_stopSearch),
            _isSearching.Select(x => !x));
        SearchCommand.Subscribe(x => OnUiThread(() =>
        {
            DuplicateCaseItems =
                new ObservableCollection<DuplicateCaseViewModel>(x.Select(y => CreateDuplicateCase(y.Key, y.Value)));
            DuplicateCase = DuplicateCaseItems.FirstOrDefault();
            Finished = true;
        }));
        SelectFolderCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            string? selectedPath = await _folderPicker.PickFolderAsync(SearchPath);

            return selectedPath != null && Directory.Exists(selectedPath) ? selectedPath : SearchPath;
        }, SearchCommand.IsExecuting.Select(x => !x));
        SelectFolderCommand.Subscribe(path => { SearchPath = path; });
        StopSearchCommand = ReactiveCommand.Create(
            () => _stopSearch.OnNext(Unit.Default),
            _isSearching);
        ApplyRuleCommand = ReactiveCommand.Create(ApplyRule, _isSearching.Select(x => !x));
        DeleteNonKeepersCommand = ReactiveCommand.CreateFromTask(DeleteNonKeepersAsync,
            _isSearching.Select(x => !x));
        DeleteSelectedNonKeepersCommand = ReactiveCommand.CreateFromTask(DeleteSelectedNonKeepersAsync,
            _isSearching.Select(x => !x));

        _duplicateCaseItems = new();
    }

    private async Task<Dictionary<HashKey, HashSet<DuplicateFile>>> SearchDuplicate(CancellationToken ct)
    {
        _isSearching.OnNext(true);

        try
        {
            OnUiThread(() =>
            {
                ProgressValue = 0;
                SearchLog = string.Empty;
                Finished = false;
                DuplicateCase = null;
                _duplicateCaseItems.Clear();
            });

            return await Task.Run(() => SearchFunction(ct), ct);
        }
        catch (OperationCanceledException)
        {
            return new Dictionary<HashKey, HashSet<DuplicateFile>>();
        }
        catch (Exception ex)
        {
            OnUiThread(() => SearchLog = $"{SearchLog}{Environment.NewLine}{ex}");
            return new Dictionary<HashKey, HashSet<DuplicateFile>>();
        }
        finally
        {
            OnUiThread(() => Finished = true);
            _isSearching.OnNext(false);
        }
    }

    protected abstract Dictionary<HashKey, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct);

    protected IDeleteConfirmation DeleteConfirmation => _deleteConfirmation;

    protected virtual DuplicateCaseViewModel CreateDuplicateCase(HashKey key, IEnumerable<DuplicateFile> files)
    {
        return new DuplicateCaseViewModel(this, files, _trash, _fileManagerHandler, _deleteConfirmation);
    }

    protected bool IsInTrash(string path)
    {
        return _trash.IsInTrash(path);
    }

    protected Task<string?> PickFolderAsync()
    {
        return _folderPicker.PickFolderAsync(SearchPath);
    }

    protected FileSearchFilter CreateSearchFilter()
    {
        long minimumSize = 0;

        if (!string.IsNullOrWhiteSpace(MinimumFileSizeText) &&
            !long.TryParse(MinimumFileSizeText, out minimumSize))
        {
            throw new FormatException("Minimum file size must be a number of bytes.");
        }

        return FileSearchFilter.FromText(minimumSize, FileExtensions, ExcludedFolders);
    }

    private void ApplyRule()
    {
        if (!Finished || _isDeletingNonKeepers || AutoDeleteRule == AutoDeleteRule.Manual)
        {
            return;
        }

        foreach (DuplicateCaseViewModel duplicateCase in DuplicateCaseItems)
        {
            DuplicateCaseItemViewModel? keeper = AutoDeleteRule switch
            {
                AutoDeleteRule.KeepOldest => duplicateCase.Files
                    .OrderBy(file => file.LastWriteTimeUtc)
                    .FirstOrDefault(),
                AutoDeleteRule.KeepNewest => duplicateCase.Files
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .FirstOrDefault(),
                AutoDeleteRule.KeepShortestPath => duplicateCase.Files
                    .OrderBy(file => file.FilePath.Length)
                    .FirstOrDefault(),
                AutoDeleteRule.KeepLongestPath => duplicateCase.Files
                    .OrderByDescending(file => file.FilePath.Length)
                    .FirstOrDefault(),
                _ => null
            };

            if (keeper != null)
            {
                duplicateCase.SetKeeper(keeper);
            }
        }
    }

    private Task DeleteNonKeepersAsync()
    {
        if (!Finished || _isDeletingNonKeepers)
        {
            return Task.CompletedTask;
        }

        List<DuplicateCaseItemViewModel> items = DuplicateCaseItems
            .SelectMany(duplicateCase => duplicateCase.Files)
            .Where(file => file.CanDelete && !file.IsKeeper)
            .ToList();

        return DeleteItemsAsync(items, "all groups");
    }

    private Task DeleteSelectedNonKeepersAsync()
    {
        if (!Finished || _isDeletingNonKeepers || DuplicateCase is null)
        {
            return Task.CompletedTask;
        }

        List<DuplicateCaseItemViewModel> items = DuplicateCase.Files
            .Where(file => file.CanDelete && !file.IsKeeper)
            .ToList();

        return DeleteItemsAsync(items, "the selected group");
    }

    private async Task DeleteItemsAsync(List<DuplicateCaseItemViewModel> items, string scope)
    {
        if (items.Count == 0)
        {
            return;
        }

        if (DryRun)
        {
            SearchLog = $"{SearchLog}{Environment.NewLine}Dry run: would move {items.Count} duplicate(s) " +
                        $"from {scope} to the trash.";
            return;
        }

        bool confirmed = await _deleteConfirmation.ConfirmAsync(
            SearchPath,
            $"Move {items.Count} non-keeper file(s) from {scope} to the trash?");

        if (!confirmed)
        {
            return;
        }

        _isDeletingNonKeepers = true;

        try
        {
            int deleted = 0;
            int failed = 0;

            foreach (DuplicateCaseItemViewModel item in items)
            {
                if (item.IsKeeper || !item.CanDelete)
                {
                    continue;
                }

                try
                {
                    await _trash.DeleteAsync(
                        item.FilePath,
                        item.FileLength,
                        item.LastWriteTimeUtc);
                    item.SetDeleted(true);
                    deleted++;
                }
                catch (Exception ex)
                {
                    failed++;
                    SearchLog = $"{SearchLog}{Environment.NewLine}{ex.Message}";
                }
            }

            SearchLog = $"{SearchLog}{Environment.NewLine}Moved {deleted} file(s); {failed} failed.";
        }
        finally
        {
            _isDeletingNonKeepers = false;
        }
    }

    public void Remove(DuplicateCaseViewModel duplicateCaseViewModel)
    {
        DuplicateCaseViewModel? currentSelection = DuplicateCase;
        int currentSelectedIndex = DuplicateCaseItems.IndexOf(duplicateCaseViewModel);
        DuplicateCaseItems.Remove(duplicateCaseViewModel);

        if (DuplicateCaseItems.Count == 0)
        {
            DuplicateCase = null;
            return;
        }

        if (currentSelection != null && DuplicateCaseItems.Contains(currentSelection))
        {
            return;
        }

        int selectedIndex = currentSelectedIndex < 0
            ? 0
            : Math.Min(currentSelectedIndex, DuplicateCaseItems.Count - 1);

        DuplicateCase = DuplicateCaseItems[selectedIndex];
    }
}
