using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DuplicateAssistant.Business;
using DuplicateAssistant.Business.FileHashing;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class DeleteIfElsewhereViewModel : ViewModelBase
{
    private readonly Trash _trash;
    private readonly IDeleteConfirmation _deleteConfirmation;
    private readonly object _progressLock = new();
    private CancellationTokenSource? _cancellation;
    private int _pendingProgress;
    private bool _progressUpdateScheduled;
    private string _sourcePath;
    private string _otherPath;
    private string _hashAlgorithm = "Blake3";
    private string _status = "Ready.";
    private bool _isBusy;
    private int _progressValue;

    public DeleteIfElsewhereViewModel(Trash trash, string sourcePath, IDeleteConfirmation deleteConfirmation)
    {
        _trash = trash;
        _sourcePath = sourcePath;
        _otherPath = string.Empty;
        _deleteConfirmation = deleteConfirmation;
        HashAlgorithms = new List<string>(HashAlgorithmFactory.Names);
        Duplicates = new ObservableCollection<CrossFolderDuplicate>();

        BrowseSourceCommand = ReactiveCommand.CreateFromTask(() => BrowseAsync(path => SourcePath = path, SourcePath));
        BrowseOtherCommand = ReactiveCommand.CreateFromTask(() => BrowseAsync(path => OtherPath = path, OtherPath));
        ScanCommand = ReactiveCommand.CreateFromTask(ScanAsync);
        StopCommand = ReactiveCommand.Create(Stop);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteAsync);
    }

    public List<string> HashAlgorithms { get; }

    public string HashAlgorithm
    {
        get => _hashAlgorithm;
        set => this.RaiseAndSetIfChanged(ref _hashAlgorithm, value);
    }

    public string SourcePath
    {
        get => _sourcePath;
        set => this.RaiseAndSetIfChanged(ref _sourcePath, value);
    }

    public string OtherPath
    {
        get => _otherPath;
        set => this.RaiseAndSetIfChanged(ref _otherPath, value);
    }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public int ProgressValue
    {
        get => _progressValue;
        private set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    public ObservableCollection<CrossFolderDuplicate> Duplicates { get; }

    public bool HasResults => Duplicates.Count > 0;

    public ReactiveCommand<Unit, Unit> BrowseSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseOtherCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    private async Task BrowseAsync(Action<string> setPath, string currentPath)
    {
        Window mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                            ?? throw new InvalidCastException("MainWindow not found!");

        IStorageFolder? suggestedStartLocation = Directory.Exists(currentPath)
            ? await mainWindow.StorageProvider.TryGetFolderFromPathAsync(currentPath)
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
            setPath(selectedPath);
        }
    }

    private async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!Directory.Exists(SourcePath) || !Directory.Exists(OtherPath))
        {
            Status = "Choose two existing folders.";
            return;
        }

        IsBusy = true;
        Status = "Scanning...";
        ProgressValue = 0;
        Duplicates.Clear();
        RaiseHasResultsChanged();
        _cancellation = new CancellationTokenSource();

        try
        {
            IHashFile hashing = HashAlgorithmFactory.Create(HashAlgorithm);
            CancellationToken cancellationToken = _cancellation.Token;

            IReadOnlyList<CrossFolderDuplicate> results = await Task.Run(
                () => CrossFolderDuplicateFinder.Find(
                    SourcePath,
                    OtherPath,
                    fileInfo => !fileInfo.IsLink() && !fileInfo.IsFifoFile(),
                    path => hashing.Hash(path, cancellationToken),
                    TextWriter.Null,
                    UpdateProgress,
                    cancellationToken),
                cancellationToken);

            OnUiThread(() =>
            {
                foreach (CrossFolderDuplicate duplicate in results)
                {
                    Duplicates.Add(duplicate);
                }

                ProgressValue = 100;
                Status = $"{results.Count} file(s) in the source folder also exist elsewhere.";
                RaiseHasResultsChanged();
            });
        }
        catch (OperationCanceledException)
        {
            OnUiThread(() => Status = "Stopped.");
        }
        catch (Exception ex)
        {
            OnUiThread(() => Status = ex.Message);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            OnUiThread(() => IsBusy = false);
        }
    }

    private void Stop()
    {
        _cancellation?.Cancel();
    }

    private async Task DeleteAsync()
    {
        if (IsBusy || Duplicates.Count == 0)
        {
            return;
        }

        bool confirmed = await _deleteConfirmation.ConfirmAsync(
            SourcePath,
            $"Move {Duplicates.Count} file(s) from the source folder to the trash?");

        if (!confirmed)
        {
            return;
        }

        List<CrossFolderDuplicate> duplicates = Duplicates.ToList();
        IsBusy = true;
        Status = "Moving files to the trash...";
        _cancellation = new CancellationTokenSource();
        int deleted = 0;
        int failed = 0;

        try
        {
            foreach (CrossFolderDuplicate duplicate in duplicates)
            {
                _cancellation.Token.ThrowIfCancellationRequested();

                try
                {
                    await _trash.DeleteAsync(
                        duplicate.Source.Path,
                        duplicate.Source.Length,
                        duplicate.Source.LastWriteTimeUtc,
                        _cancellation.Token);
                    deleted++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                }
            }

            OnUiThread(() =>
            {
                Duplicates.Clear();
                Status = failed == 0
                    ? $"Moved {deleted} file(s) to the trash."
                    : $"Moved {deleted} file(s); {failed} could not be moved.";
                RaiseHasResultsChanged();
            });
        }
        catch (OperationCanceledException)
        {
            OnUiThread(() => Status = $"Stopped after moving {deleted} file(s).");
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            OnUiThread(() => IsBusy = false);
        }
    }

    private void UpdateProgress(int progress)
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

    private void RaiseHasResultsChanged()
    {
        this.RaisePropertyChanged(nameof(HasResults));
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
