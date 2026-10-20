using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using DuplicateAssistant.Business;
using DuplicateAssistant.Business.FileHashing;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class MultiFolderDuplicateViewModel : DuplicateInFolderViewModel
{
    private readonly ScanCache? _scanCache;
    private string? _selectedFolder;

    public MultiFolderDuplicateViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler,
        IDeleteConfirmation deleteConfirmation, IFolderPicker folderPicker, ScanCache? scanCache = null) :
        base(trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker)
    {
        _scanCache = scanCache;
        Folders = new ObservableCollection<string> { searchPath };
        AddFolderCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (!Finished)
            {
                return;
            }

            string? selectedPath = await PickFolderAsync();

            if (selectedPath != null &&
                !Folders.Contains(selectedPath, PathUtilities.PathComparer))
            {
                Folders.Add(selectedPath);
            }
        });
        RemoveFolderCommand = ReactiveCommand.Create(() =>
        {
            if (!Finished)
            {
                return;
            }

            if (SelectedFolder != null)
            {
                Folders.Remove(SelectedFolder);
                SelectedFolder = null;
            }
        });
    }

    public ObservableCollection<string> Folders { get; }

    public string? SelectedFolder
    {
        get => _selectedFolder;
        set => this.RaiseAndSetIfChanged(ref _selectedFolder, value);
    }

    public ReactiveCommand<Unit, Unit> AddFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveFolderCommand { get; }

    protected override Dictionary<HashKey, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        using ControlWriter textDisplayProgress = new(this);

        IHashFile fileHashing = HashAlgorithmFactory.Create(HashAlgorithm);
        FileSearchFilter searchFilter = CreateSearchFilter();

        byte[] Compute(string path)
        {
            byte[] result = fileHashing.Hash(path, ct);
            textDisplayProgress.Write($"Hashing {path}... ");
            textDisplayProgress.WriteLine(HashFormatter.Format(result));
            return result;
        }

        Func<string, byte[]> hash = _scanCache is null
            ? Compute
            : path => _scanCache.GetOrCompute(HashAlgorithm, path, Compute);

        try
        {
            return MultiFolderDuplicateFinder.Find(
                Folders.ToList(),
                searchOption,
                searchFilter,
                hash,
                textDisplayProgress,
                UpdateProgress,
                ct,
                path => !IsInTrash(path));
        }
        finally
        {
            _scanCache?.Save();
        }
    }
}
