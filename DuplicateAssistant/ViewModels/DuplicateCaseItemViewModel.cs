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
    private readonly IDeleteConfirmation _deleteConfirmation;
    private readonly long _fileLength;
    private readonly DateTime _lastWriteTimeUtc;
    private string? _trashedPath;
    private bool _isKeeper;
    private bool _isDeleted;
    private string _errorMessage = string.Empty;
    public string FilePath { get; }
    public long FileSize => _fileLength;
    public long FileLength => _fileLength;
    public DateTime LastWriteTimeUtc => _lastWriteTimeUtc;
    public bool IsKeeper => _isKeeper;
    public bool IsDeleted => _isDeleted;
    public bool CanDelete => !IsDeleted && !IsKeeper;
    public bool CanHardlink => CanDelete;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(HasError));
        }
    }
    public ReactiveCommand<string, Unit> RevealInFolderCommand { get; }
    public ReactiveCommand<string, Unit> OpenCommand { get; }
    public ReactiveCommand<string, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> RestoreCommand { get; }
    public ReactiveCommand<Unit, Unit> MakeKeeperCommand { get; }
    public ReactiveCommand<Unit, Unit> HardlinkCommand { get; }

    public DuplicateCaseItemViewModel(DuplicateCaseViewModel duplicateCase, DuplicateFile file,
        FileManagerHandler fileManagerHandler, Trash trash, IDeleteConfirmation deleteConfirmation)
    {
        _duplicateCase = duplicateCase;
        FilePath = file.Path;
        _trash = trash;
        _fileManagerHandler = fileManagerHandler;
        _deleteConfirmation = deleteConfirmation;
        _fileLength = file.Length;
        _lastWriteTimeUtc = file.LastWriteTimeUtc;

        RevealInFolderCommand = ReactiveCommand.CreateFromTask<string>(fileManagerHandler.RevealFileInFolder);
        OpenCommand = ReactiveCommand.CreateFromTask<string>(_fileManagerHandler.OpenFile);
        DeleteCommand = ReactiveCommand.CreateFromTask<string>(DeleteDuplicateItem);
        RestoreCommand = ReactiveCommand.CreateFromTask(RestoreFile);
        MakeKeeperCommand = ReactiveCommand.Create(() => _duplicateCase.SetKeeper(this));
        HardlinkCommand = ReactiveCommand.CreateFromTask(HardlinkAsync);
    }

    internal void ApplyKeeper(bool value)
    {
        this.RaiseAndSetIfChanged(ref _isKeeper, value, nameof(IsKeeper));
        this.RaisePropertyChanged(nameof(CanDelete));
        this.RaisePropertyChanged(nameof(CanHardlink));
    }

    internal void SetDeleted(bool value)
    {
        this.RaiseAndSetIfChanged(ref _isDeleted, value, nameof(IsDeleted));
        this.RaisePropertyChanged(nameof(CanDelete));
        this.RaisePropertyChanged(nameof(CanHardlink));
    }

    private async Task HardlinkAsync()
    {
        DuplicateCaseItemViewModel? keeper = _duplicateCase.Keeper;

        if (!CanHardlink || keeper is null || ReferenceEquals(keeper, this))
        {
            return;
        }

        bool confirmed = await _deleteConfirmation.ConfirmAsync(
            FilePath,
            $"Replace this duplicate with a hard link to the keeper?\n\nKeeper: {keeper.FilePath}");

        if (!confirmed)
        {
            return;
        }

        try
        {
            string trashedPath = await _trash.DeleteAsync(FilePath, _fileLength, _lastWriteTimeUtc);

            try
            {
                HardLinkService.Create(FilePath, keeper.FilePath);
            }
            catch (Exception ex)
            {
                try
                {
                    await _trash.RestoreAsync(trashedPath, FilePath);
                }
                catch (Exception restoreException)
                {
                    ErrorMessage = $"{ex.Message} Restore failed: {restoreException.Message}";
                    return;
                }

                ErrorMessage = ex.Message;
                return;
            }

            try
            {
                _trash.Discard(trashedPath);
            }
            catch
            {
                // The link is in place; keeping the trash entry is safer than losing the old inode mapping.
            }

            _duplicateCase.Remove(this);
            ErrorMessage = string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task DeleteDuplicateItem(string x)
    {
        if (!CanDelete || !await _deleteConfirmation.ConfirmAsync(FilePath))
        {
            return;
        }

        try
        {
            _trashedPath = await _trash.DeleteAsync(x, _fileLength, _lastWriteTimeUtc);
            SetDeleted(true);
            ErrorMessage = string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task RestoreFile()
    {
        if (!IsDeleted || _trashedPath is null)
        {
            return;
        }

        try
        {
            await _trash.RestoreAsync(_trashedPath, FilePath);
            _trashedPath = null;
            SetDeleted(false);
            ErrorMessage = string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
        }
    }
}