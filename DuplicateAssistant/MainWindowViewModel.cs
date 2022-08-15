using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using FileCompare;
using ReactiveUI;

namespace DuplicateAssitant;

public class DuplicateInFolderViewModel : ViewModelBase
{
      private readonly Trash _trash;
    public ReactiveCommand<Unit, Dictionary<string, HashSet<string>>> Search { get; }
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

    public DuplicateInFolderViewModel(Trash trash, string searchPath)
    {
        _trash = trash;
        _duplicateCaseItems = new ObservableCollection<DuplicateCaseViewModel>() { };
        SearchPath = searchPath;
        Search = ReactiveCommand.CreateFromTask(SearchDuplicate);
        Search.Subscribe(x =>
        {
            DuplicateCaseItems = new ObservableCollection<DuplicateCaseViewModel>(x.Select(x =>
                    new DuplicateCaseViewModel(x.Value)));
        });
        Open = ReactiveCommand.CreateFromTask<string>(OpenFile);
        Delete = ReactiveCommand.CreateFromTask<string>(DeleteDuplicateItem);
    }

    private async Task<Dictionary<string, HashSet<string>>> SearchDuplicate()
    {
        ProgressValue = 0;
        _duplicateCaseItems.Clear();
        return await Task.Run(() => DupProvider.FindDuplicateByHash(SearchPath, SearchOption.AllDirectories, Console.Out, progress => { ProgressValue = progress; }));
    }

    private async Task OpenFile(string x)
    {
        using Process fileOpener = new Process();

        fileOpener.StartInfo.FileName = "explorer";
        fileOpener.StartInfo.Arguments = "\"" + x + "\"";
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

public class MainWindowViewModel : ViewModelBase
{
    public DuplicateInFolderViewModel DuplicateInFolderViewModel { get; }

    public MainWindowViewModel(DuplicateInFolderViewModel duplicateInFolderViewModel)
    {
        DuplicateInFolderViewModel = duplicateInFolderViewModel;
    }
}