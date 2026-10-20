using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using DuplicateAssistant.Business;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class DuplicateCaseItemViewModel : ViewModelBase
{
    private readonly Trash _trash;
    private readonly DuplicateCaseViewModel _duplicateCase;
    private readonly FileManagerHandler _fileManagerHandler;
    private readonly long _fileLength;
    private readonly DateTime _lastWriteTimeUtc;
    private bool _isKeeper;
    private bool _isDeleted;
    public string FilePath { get; }
    public long FileSize { get; }
    public bool IsKeeper => _isKeeper;
    public bool IsDeleted => _isDeleted;
    public ReactiveCommand<string, Unit> RevealInFolderCommand { get; }
    public ReactiveCommand<string, Unit> OpenCommand { get; }
    public ReactiveCommand<string, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> MakeKeeperCommand { get; }

    public DuplicateCaseItemViewModel(DuplicateCaseViewModel duplicateCase, DuplicateFile file, FileManagerHandler fileManagerHandler, Trash trash)
    {
        _duplicateCase = duplicateCase;
        FilePath = file.Path;
        FileSize = file.Length;
        _trash = trash;
        _fileManagerHandler = fileManagerHandler;
        _fileLength = file.Length;
        _lastWriteTimeUtc = file.LastWriteTimeUtc;

        RevealInFolderCommand = ReactiveCommand.CreateFromTask<string>(fileManagerHandler.RevealFileInFolder);
        OpenCommand = ReactiveCommand.CreateFromTask<string>(_fileManagerHandler.OpenFile);
        DeleteCommand = ReactiveCommand.CreateFromTask<string>(DeleteDuplicateItem);
        MakeKeeperCommand = ReactiveCommand.Create(() => _duplicateCase.SetKeeper(this));
    }

    internal void ApplyKeeper(bool value)
    {
        this.RaiseAndSetIfChanged(ref _isKeeper, value, nameof(IsKeeper));
    }

    internal void SetDeleted(bool value)
    {
        this.RaiseAndSetIfChanged(ref _isDeleted, value, nameof(IsDeleted));
    }

    private async Task DeleteDuplicateItem(string x)
    {
        _trash.Delete(x, _fileLength, _lastWriteTimeUtc);
        _duplicateCase.Remove(this);
        await Task.CompletedTask;
    }
}