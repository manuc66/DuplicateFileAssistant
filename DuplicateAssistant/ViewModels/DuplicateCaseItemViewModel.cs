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
    public string FilePath { get; }
    public ReactiveCommand<string, Unit> RevealInFolderCommand { get; }
    public ReactiveCommand<string, Unit> OpenCommand { get; }
    public ReactiveCommand<string, Unit> DeleteCommand { get; }

    public DuplicateCaseItemViewModel(DuplicateCaseViewModel duplicateCase, string filePath, FileManagerHandler fileManagerHandler, Trash trash)
    {
        _duplicateCase = duplicateCase;
        FilePath = filePath;
        _trash = trash;
        _fileManagerHandler = fileManagerHandler;

        FileInfo fileInfo = new(filePath);
        if (fileInfo.Exists)
        {
            _fileLength = fileInfo.Length;
            _lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        }
        else
        {
            _fileLength = -1;
            _lastWriteTimeUtc = DateTime.MinValue;
        }

        RevealInFolderCommand = ReactiveCommand.CreateFromTask<string>(fileManagerHandler.RevealFileInFolder);
        OpenCommand = ReactiveCommand.CreateFromTask<string>(_fileManagerHandler.OpenFile);
        DeleteCommand = ReactiveCommand.CreateFromTask<string>(DeleteDuplicateItem);
    }

    private async Task DeleteDuplicateItem(string x)
    {
        _trash.Delete(x, _fileLength, _lastWriteTimeUtc);
        _duplicateCase.Remove(this);
        await Task.CompletedTask;
    }
}