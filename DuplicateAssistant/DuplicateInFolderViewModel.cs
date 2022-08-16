using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileCompare;
using ReactiveUI;

namespace DuplicateAssistant;

public class DuplicateInFolderViewModel : ViewModelBase
{
    private readonly Trash _trash;
    public ReactiveCommand<Unit, Dictionary<string, HashSet<string>>> Search { get; }
    public ReactiveCommand<Unit, Unit> StopSearch { get; }
    public ReactiveCommand<string, Unit>  RevealInFolder{ get; }
    public ReactiveCommand<string, Unit> Open { get; }
    public ReactiveCommand<string, Unit> Delete { get; }

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

    private DuplicateCaseViewModel _duplicateCase;
    public DuplicateCaseViewModel DuplicateCase
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

    public DuplicateInFolderViewModel(Trash trash, string searchPath)
    {
        _trash = trash;
        SearchPath = searchPath;
        SubFolder = true;
        Search = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(SearchDuplicate)
                .TakeUntil(StopSearch!));
        Search.Subscribe(x =>
        {
            DuplicateCaseItems = new ObservableCollection<DuplicateCaseViewModel>(x.Select(x => new DuplicateCaseViewModel(x.Value)));
        });
        StopSearch =  ReactiveCommand.Create(
            () => { },
            Search.IsExecuting);
        
        _duplicateCaseItems = new ObservableCollection<DuplicateCaseViewModel>();
        
        RevealInFolder = ReactiveCommand.CreateFromTask<string>(RevealFileInFolder);
        Open = ReactiveCommand.CreateFromTask<string>(OpenFile);
        Delete = ReactiveCommand.CreateFromTask<string>(DeleteDuplicateItem);
    }

    private async Task<Dictionary<string, HashSet<string>>> SearchDuplicate(CancellationToken ct)
    {
        ProgressValue = 0;
        _duplicateCaseItems.Clear();
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return await Task.Run(() => DupProvider.FindDuplicateByHash(SearchPath, searchOption, Console.Out, progress => { ProgressValue = progress; }, ct), ct);
    }

    private async Task RevealFileInFolder(string path)
    {
        using Process fileOpener = new Process();
        fileOpener.StartInfo.FileName = "explorer";
        fileOpener.StartInfo.Arguments = "/select," + path + "\"";
        fileOpener.Start();
        await fileOpener.WaitForExitAsync();
    }

    private async Task OpenFile(string path)
    {
        using Process fileOpener = new Process();

        fileOpener.StartInfo.FileName = "explorer";
        fileOpener.StartInfo.Arguments = "\"" + path + "\"";
        fileOpener.Start();
        await fileOpener.WaitForExitAsync();
    }

    private async Task DeleteDuplicateItem(string x)
    {
        _trash.Delete(x);
        DuplicateCase.Files.Remove(x);
        if (DuplicateCase.Files.Count == 1)
        {
            int currentSelectedIndex = DuplicateCaseItems.IndexOf(DuplicateCase);
            DuplicateCaseItems.Remove(DuplicateCase);
            DuplicateCase = DuplicateCaseItems[Math.Min(currentSelectedIndex, DuplicateCaseItems.Count - 1)];
        }
    }
}