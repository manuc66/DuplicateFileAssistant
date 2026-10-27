using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using DuplicateAssistant.Business;
using DuplicateAssistant.Business.FileHashing;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class DeleteIfElsewhereViewModel : ViewModelBase
{
    private readonly Trash _trash;
    private readonly IDeleteConfirmation _deleteConfirmation;
    private readonly IFolderPicker _folderPicker;
    private readonly ScanCache? _scanCache;
    private string _sourcePath;
    private string _otherPath;
    private string _hashAlgorithm = "Blake3";
    private bool _dryRun;

    public DeleteIfElsewhereViewModel(Trash trash, string sourcePath, IDeleteConfirmation deleteConfirmation,
        IFolderPicker folderPicker, ScanCache? scanCache = null)
    {
        _trash = trash;
        _sourcePath = sourcePath;
        _otherPath = string.Empty;
        _deleteConfirmation = deleteConfirmation;
        _folderPicker = folderPicker;
        _scanCache = scanCache;
        HashAlgorithms = new List<string>(HashAlgorithmFactory.Names);
        Duplicates = new ObservableCollection<CrossFolderDuplicate>();

        BrowseSourceCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            string? selectedPath = await _folderPicker.PickFolderAsync(SourcePath);
            if (selectedPath != null)
            {
                SourcePath = selectedPath;
            }
        });
        BrowseOtherCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            string? selectedPath = await _folderPicker.PickFolderAsync(OtherPath);
            if (selectedPath != null)
            {
                OtherPath = selectedPath;
            }
        });
        ScanCommand = ReactiveCommand.CreateFromTask(ScanAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteAsync);
    }

    public List<string> HashAlgorithms { get; }

    public string HashAlgorithm
    {
        get => _hashAlgorithm;
        set => this.RaiseAndSetIfChanged(ref _hashAlgorithm, value);
    }

    public bool DryRun
    {
        get => _dryRun;
        set => this.RaiseAndSetIfChanged(ref _dryRun, value);
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

    public ObservableCollection<CrossFolderDuplicate> Duplicates { get; }

    public bool HasResults => Duplicates.Count > 0;

    public ReactiveCommand<Unit, Unit> BrowseSourceCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseOtherCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

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

        ProgressValue = 0;
        Duplicates.Clear();
        RaiseHasResultsChanged();

        await RunOperationAsync(async cancellationToken =>
        {
            IHashFile hashing = HashAlgorithmFactory.Create(HashAlgorithm);

            IReadOnlyList<CrossFolderDuplicate> results = await Task.Run(
                () =>
                {
                    byte[] Compute(string path) => hashing.Hash(path, cancellationToken);
                    Func<string, byte[]> hash = _scanCache is null
                        ? Compute
                        : path => _scanCache.GetOrCompute(HashAlgorithm, path, Compute);

                    try
                    {
                        return CrossFolderDuplicateFinder.Find(
                            SourcePath,
                            OtherPath,
                            fileInfo => !fileInfo.IsLink() && !fileInfo.IsFifoFile(),
                            hash,
                            TextWriter.Null,
                            UpdateProgress,
                            cancellationToken,
                            path => !_trash.IsInTrash(path));
                    }
                    finally
                    {
                        _scanCache?.Save();
                    }
                },
                cancellationToken);

            OnUiThread(() =>
            {
                foreach (CrossFolderDuplicate duplicate in results)
                {
                    Duplicates.Add(duplicate);
                }

                UpdateProgress(100);
                Status = $"{results.Count} file(s) in the source folder also exist elsewhere.";
                RaiseHasResultsChanged();
            });
        }, "Scanning...");
    }

    private async Task DeleteAsync()
    {
        if (IsBusy || Duplicates.Count == 0)
        {
            return;
        }

        if (DryRun)
        {
            Status = $"Dry run: would move {Duplicates.Count} file(s) to the trash.";
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

        await RunOperationAsync(async cancellationToken =>
        {
            int deleted = 0;
            int failed = 0;
            int processed = 0;
            List<CrossFolderDuplicate> deletedDuplicates = new();

            try
            {
                foreach (CrossFolderDuplicate duplicate in duplicates)
                {
                    await PauseSignal.WaitWhilePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    ReportProgress(processed, duplicates.Count, duplicate.Source.Path);
                    processed++;

                    try
                    {
                        await _trash.DeleteAsync(
                            duplicate.Source.Path,
                            duplicate.Source.Length,
                            duplicate.Source.LastWriteTimeUtc,
                            cancellationToken);
                        deleted++;
                        deletedDuplicates.Add(duplicate);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        failed++;
                    }
                }
            }
            finally
            {
                OnUiThread(() =>
                {
                    foreach (CrossFolderDuplicate duplicate in deletedDuplicates)
                    {
                        Duplicates.Remove(duplicate);
                    }

                    RaiseHasResultsChanged();
                });
            }

            ReportProgress(duplicates.Count, duplicates.Count);

            OnUiThread(() =>
            {
                Duplicates.Clear();
                Status = failed == 0
                    ? $"Moved {deleted} file(s) to the trash."
                    : $"Moved {deleted} file(s); {failed} could not be moved.";
                RaiseHasResultsChanged();
            });
        }, "Moving files to the trash...", canPause: true);
    }

    private void RaiseHasResultsChanged()
    {
        this.RaisePropertyChanged(nameof(HasResults));
    }
}
