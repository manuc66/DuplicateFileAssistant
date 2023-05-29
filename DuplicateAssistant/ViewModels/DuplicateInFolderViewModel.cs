using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DuplicateAssistant.Business;
using FileCompare;
using ReactiveUI;

namespace DuplicateAssistant.ViewModels;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public abstract class DuplicateInFolderViewModel : ViewModelBase, IHaveSearchLog
{
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

    public string NewLine { get; }


    protected DuplicateInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler)
    {
        _searchPath = searchPath;
        _searchLog = string.Empty;
        SubFolder = true;
        Finished = true;
        SearchCommand = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(SearchDuplicate)
                .TakeUntil(StopSearchCommand!));
        SearchCommand.Subscribe(x =>
        {
            DuplicateCaseItems =
                new ObservableCollection<DuplicateCaseViewModel>(x.Select(y => new DuplicateCaseViewModel(this, y.Value, trash, fileManagerHandler)));
            Finished = true;
        });
        SelectFolderCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            OpenFolderDialog ofg = new()
            {
                Directory = SearchPath
            };

            Window mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                                ?? throw new InvalidCastException("MainWindow not found!");
            string? selectedPath = await ofg.ShowAsync(mainWindow);

            if (selectedPath != null && Directory.Exists(selectedPath))
            {
                return selectedPath;
            }

            return SearchPath;
        }, SearchCommand.IsExecuting.Select(x => !x));
        SelectFolderCommand.Subscribe(path => { SearchPath = path; });
        StopSearchCommand = ReactiveCommand.Create(
            () => { },
            SearchCommand.IsExecuting);

        _duplicateCaseItems = new();
    }

    private async Task<Dictionary<string, HashSet<string>>> SearchDuplicate(CancellationToken ct)
    {
        ProgressValue = 0;
        SearchLog = string.Empty;
        Finished = false;
        _duplicateCaseItems.Clear();
        return await Task.Run(() => SearchFunction(ct), ct);
    }

    protected abstract Dictionary<string, HashSet<string>> SearchFunction(CancellationToken ct);

    public void Remove(DuplicateCaseViewModel duplicateCaseViewModel)
    {
        int currentSelectedIndex = DuplicateCaseItems.IndexOf(duplicateCaseViewModel);
        DuplicateCaseItems.Remove(duplicateCaseViewModel);
        DuplicateCase = DuplicateCaseItems[Math.Min(currentSelectedIndex, DuplicateCaseItems.Count - 1)];
    }
}