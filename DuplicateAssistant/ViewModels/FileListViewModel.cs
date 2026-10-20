using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using DuplicateAssistant.Business;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public enum FileListMode
{
    EmptyFiles,
    BigFiles,
    TemporaryFiles
}

public class FileListViewModel : ViewModelBase
{
    private readonly Trash _trash;
    private readonly IDeleteConfirmation _deleteConfirmation;
    private readonly IFolderPicker _folderPicker;
    private readonly FileListMode _mode;
    private readonly LastFolderStore? _lastFolderStore;
    private string _searchPath;
    private int _maximumFiles = 100;
    private bool _dryRun;
    private DuplicateFile? _selectedFile;
    private string _minimumSizeText = string.Empty;
    private string _maximumSizeText = string.Empty;

    public FileListViewModel(Trash trash, IDeleteConfirmation deleteConfirmation, IFolderPicker folderPicker,
        FileListMode mode, string searchPath, LastFolderStore? lastFolderStore = null)
    {
        _trash = trash;
        _deleteConfirmation = deleteConfirmation;
        _folderPicker = folderPicker;
        _mode = mode;
        _lastFolderStore = lastFolderStore;
        _searchPath = searchPath;
        ExtensionSelection = new ExtensionSelectionViewModel();
        ExtensionSelection.SelectionChanged += (_, _) => this.RaisePropertyChanged(nameof(FileExtensions));
        Files = new ObservableCollection<DuplicateFile>();

        SelectFolderCommand = ReactiveCommand.CreateFromTask(SelectFolderAsync);
        ScanCommand = ReactiveCommand.CreateFromTask(ScanAsync);
        StopCommand = ReactiveCommand.Create(StopOperation);
        DeleteSelectedCommand = ReactiveCommand.CreateFromTask(() => DeleteAsync(all: false));
        DeleteAllCommand = ReactiveCommand.CreateFromTask(() => DeleteAsync(all: true));
    }

    public string SearchPath
    {
        get => _searchPath;
        set => this.RaiseAndSetIfChanged(ref _searchPath, value);
    }

    public int MaximumFiles
    {
        get => _maximumFiles;
        set => this.RaiseAndSetIfChanged(ref _maximumFiles, value);
    }

    public bool DryRun
    {
        get => _dryRun;
        set => this.RaiseAndSetIfChanged(ref _dryRun, value);
    }

    public DuplicateFile? SelectedFile
    {
        get => _selectedFile;
        set => this.RaiseAndSetIfChanged(ref _selectedFile, value);
    }

    public string MinimumSizeText
    {
        get => _minimumSizeText;
        set => this.RaiseAndSetIfChanged(ref _minimumSizeText, value);
    }

    public string MaximumSizeText
    {
        get => _maximumSizeText;
        set => this.RaiseAndSetIfChanged(ref _maximumSizeText, value);
    }

    public bool ShowSizeFilters => _mode is FileListMode.BigFiles or FileListMode.TemporaryFiles;

    public bool ShowExtensionFilter => _mode == FileListMode.BigFiles;

    public ExtensionSelectionViewModel ExtensionSelection { get; }

    public string FileExtensions
    {
        get => ExtensionSelection.ToFilterString();
        set => ExtensionSelection.SetFromFilterString(value);
    }

    public ObservableCollection<DuplicateFile> Files { get; }
    public bool HasResults => Files.Count > 0;
    public bool ShowMaximumFiles => _mode == FileListMode.BigFiles;

    public ReactiveCommand<Unit, Unit> SelectFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteSelectedCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteAllCommand { get; }

    private async Task SelectFolderAsync()
    {
        string? selectedPath = await _folderPicker.PickFolderAsync(SearchPath);

        if (selectedPath != null)
        {
            SearchPath = selectedPath;
        }
    }

    private async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!Directory.Exists(SearchPath))
        {
            Status = "Choose an existing folder.";
            return;
        }

        _lastFolderStore?.Save(SearchPath);

        long minimumSize = 0;
        long maximumSize = long.MaxValue;

        if (_mode is FileListMode.BigFiles or FileListMode.TemporaryFiles)
        {
            if (!string.IsNullOrWhiteSpace(MinimumSizeText) &&
                !FileSizeParser.TryParse(MinimumSizeText, out minimumSize))
            {
                Status = "Minimum size must be a number, optionally followed by KB, MB, GB or TB.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(MaximumSizeText) &&
                !FileSizeParser.TryParse(MaximumSizeText, out maximumSize))
            {
                Status = "Maximum size must be a number, optionally followed by KB, MB, GB or TB.";
                return;
            }

            if (maximumSize > 0 && minimumSize > maximumSize)
            {
                Status = "Minimum size must not be greater than maximum size.";
                return;
            }
        }

        Files.Clear();
        SelectedFile = null;
        RaiseFilesChanged();

        await RunOperationAsync(async cancellationToken =>
        {
            FileSearchFilter filter = FileSearchFilter.FromText(minimumSize,
                ShowExtensionFilter ? FileExtensions : null, _trash.ScanExclusions);

            IReadOnlyList<DuplicateFile> files = await Task.Run(
                () => _mode switch
                {
                    FileListMode.EmptyFiles => EmptyFileFinder.Find(SearchPath, filter, cancellationToken),
                    FileListMode.BigFiles => BigFileFinder.Find(SearchPath, MaximumFiles, filter, maximumSize, cancellationToken),
                    FileListMode.TemporaryFiles => TemporaryFileFinder.Find(SearchPath, filter, maximumSize, cancellationToken),
                    _ => throw new ArgumentOutOfRangeException()
                },
                cancellationToken);

            OnUiThread(() =>
            {
                foreach (DuplicateFile file in files)
                {
                    Files.Add(file);
                }

                Status = $"{files.Count} file(s) found.";
                RaiseFilesChanged();
            });
        }, "Scanning...");
    }

    private async Task DeleteAsync(bool all)
    {
        if (IsBusy)
        {
            return;
        }

        List<DuplicateFile> targets = all
            ? Files.ToList()
            : SelectedFile is null ? new List<DuplicateFile>() : new List<DuplicateFile> { SelectedFile };

        if (targets.Count == 0)
        {
            return;
        }

        if (DryRun)
        {
            Status = $"Dry run: would move {targets.Count} file(s) to the trash.";
            return;
        }

        bool confirmed = await _deleteConfirmation.ConfirmAsync(
            SearchPath,
            $"Move {targets.Count} file(s) to the trash?");

        if (!confirmed)
        {
            return;
        }

        await RunOperationAsync(async cancellationToken =>
        {
            int deleted = 0;
            int failed = 0;
            List<DuplicateFile> deletedFiles = new();

            foreach (DuplicateFile file in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await _trash.DeleteAsync(file.Path, file.Length, file.LastWriteTimeUtc, cancellationToken);
                    deletedFiles.Add(file);
                    deleted++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                }
            }

            OnUiThread(() =>
            {
                foreach (DuplicateFile file in deletedFiles)
                {
                    Files.Remove(file);
                }

                SelectedFile = null;
                Status = failed == 0
                    ? $"Moved {deleted} file(s) to the trash."
                    : $"Moved {deleted} file(s); {failed} failed.";
                RaiseFilesChanged();
            });
        }, "Moving files to the trash...");
    }

    private void RaiseFilesChanged()
    {
        this.RaisePropertyChanged(nameof(HasResults));
    }
}
