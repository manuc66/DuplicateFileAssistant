using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using FileCompare;
using ReactiveUI;

namespace DuplicateAssistant;

public class DuplicateInFolderViewModel : ViewModelBase
{
    private readonly Trash _trash;

    public ReactiveCommand<Unit, string> SelectFolderCommand { get; }
    public ReactiveCommand<Unit, Dictionary<string, HashSet<string>>> SearchCommand { get; }
    public ReactiveCommand<Unit, Unit> StopSearchCommand { get; }
    public ReactiveCommand<string, Unit>  RevealInFolderCommand{ get; }
    public ReactiveCommand<string, Unit> OpenCommand { get; }
    public ReactiveCommand<string, Unit> DeleteCommand { get; }

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
    
    private bool _finished;
    public bool Finished
    {
        get => _finished;
        set
        {
            this.RaiseAndSetIfChanged(ref _finished, value);
        }
    }

    private string _searchLog;
    public string SearchLog
    {
        get => _searchLog;
        set => this.RaiseAndSetIfChanged(ref _searchLog, value);
    }


    public DuplicateInFolderViewModel(Trash trash, string searchPath)
    {
        _trash = trash;
        SearchPath = searchPath;
        SubFolder = true;
        Finished = true;
        SearchCommand = ReactiveCommand.CreateFromObservable(
            () => Observable
                .StartAsync(SearchDuplicate)
                .TakeUntil(StopSearchCommand!));
        SearchCommand.Subscribe(x =>
        {
            DuplicateCaseItems = new ObservableCollection<DuplicateCaseViewModel>(x.Select(x => new DuplicateCaseViewModel(x.Value)));
            Finished = true;
        });
        SelectFolderCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var ofg = new OpenFolderDialog
            {
                Directory = SearchPath
            };

            string? selectedPath = await ofg.ShowAsync(MainWindow.Instance);

            if (selectedPath !=  null && Directory.Exists(selectedPath))
            {
                return selectedPath;
            }
            
            return SearchPath;
        }, SearchCommand.IsExecuting.Select(x => !x));
        SelectFolderCommand.Subscribe(path =>
        {
            SearchPath = path;
        });
        StopSearchCommand =  ReactiveCommand.Create(
            () => { },
            SearchCommand.IsExecuting);
        
        _duplicateCaseItems = new ObservableCollection<DuplicateCaseViewModel>();
        
        RevealInFolderCommand = ReactiveCommand.CreateFromTask<string>(RevealFileInFolder);
        OpenCommand = ReactiveCommand.CreateFromTask<string>(OpenFile);
        DeleteCommand = ReactiveCommand.CreateFromTask<string>(DeleteDuplicateItem);
    }

    public class ControlWriter : TextWriter
    {
        private StringBuilder content;
        private readonly DuplicateInFolderViewModel _parent;

        public ControlWriter(DuplicateInFolderViewModel parent )
        {
            _parent = parent;
            content = new StringBuilder();
        }

        public override void Write(char value)
        {
            content.Append(value);
            _parent.SearchLog = content.ToString(); 
        }

        public override void Write(string value)
        {
            content.Append(value);
            _parent.SearchLog = content.ToString(); 
        }
        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }

    private async Task<Dictionary<string, HashSet<string>>> SearchDuplicate(CancellationToken ct)
    {
        ProgressValue = 0;
        SearchLog = string.Empty;
        Finished = false;
        _duplicateCaseItems.Clear();
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return await Task.Run(() => DupProvider.FindDuplicateByHash(SearchPath, searchOption, new ControlWriter(this), progress => { ProgressValue = progress; }, ct), ct);
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