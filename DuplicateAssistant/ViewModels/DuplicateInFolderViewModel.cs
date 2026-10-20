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
    private readonly IDeleteConfirmation _deleteConfirmation;
    private readonly IFolderPicker _folderPicker;
    private readonly Trash _trash;

    public ReactiveCommand<Unit, string> SelectFolderCommand { get; }
    public ReactiveCommand<Unit, Dictionary<HashKey, HashSet<DuplicateFile>>> SearchCommand { get; }
    public ReactiveCommand<Unit, Unit> StopSearchCommand { get; }

    public IReadOnlyList<string> HashAlgorithms { get; }
    public bool ShowHashOptions { get; protected set; } = true;

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
                new ObservableCollection<DuplicateCaseViewModel>(x.Select(y => new DuplicateCaseViewModel(this, y.Value, trash, fileManagerHandler, _deleteConfirmation)));
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

    protected bool IsInTrash(string path)
    {
        return PathUtilities.IsUnder(path, _trash.TrashFolder);
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
