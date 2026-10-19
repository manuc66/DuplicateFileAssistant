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
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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
    private readonly object _progressLock = new();
    private int _pendingProgress;
    private bool _progressUpdateScheduled;

    public ReactiveCommand<Unit, string> SelectFolderCommand { get; }
    public ReactiveCommand<Unit, Dictionary<string, HashSet<string>>> SearchCommand { get; }
    public ReactiveCommand<Unit, Unit> StopSearchCommand { get; }

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

    private int _progressValue;

    public int ProgressValue
    {
        get => _progressValue;
        set => this.RaiseAndSetIfChanged(ref _progressValue, value);
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

    protected DuplicateInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler)
    {
        _searchPath = searchPath;
        _searchLog = string.Empty;
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
                new ObservableCollection<DuplicateCaseViewModel>(x.Select(y => new DuplicateCaseViewModel(this, y.Value, trash, fileManagerHandler)));
            DuplicateCase = DuplicateCaseItems.FirstOrDefault();
            Finished = true;
        }));
        SelectFolderCommand = ReactiveCommand.CreateFromTask(async () =>
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
                return selectedPath;
            }

            return SearchPath;
        }, SearchCommand.IsExecuting.Select(x => !x));
        SelectFolderCommand.Subscribe(path => { SearchPath = path; });
        StopSearchCommand = ReactiveCommand.Create(
            () => _stopSearch.OnNext(Unit.Default),
            _isSearching);

        _duplicateCaseItems = new();
    }

    private async Task<Dictionary<string, HashSet<string>>> SearchDuplicate(CancellationToken ct)
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
            return new Dictionary<string, HashSet<string>>();
        }
        catch (Exception ex)
        {
            OnUiThread(() => SearchLog = $"{SearchLog}{Environment.NewLine}{ex}");
            return new Dictionary<string, HashSet<string>>();
        }
        finally
        {
            OnUiThread(() => Finished = true);
            _isSearching.OnNext(false);
        }
    }

    protected abstract Dictionary<string, HashSet<string>> SearchFunction(CancellationToken ct);

    protected void UpdateProgress(int progress)
    {
        lock (_progressLock)
        {
            _pendingProgress = progress;

            if (_progressUpdateScheduled)
            {
                return;
            }

            _progressUpdateScheduled = true;
        }

        Dispatcher.UIThread.Post(() =>
        {
            int value;

            lock (_progressLock)
            {
                value = _pendingProgress;
                _progressUpdateScheduled = false;
            }

            ProgressValue = value;
        });
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
