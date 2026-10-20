using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using DuplicateAssistant.Business;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class DuplicateCaseViewModel : ViewModelBase
{
    private readonly DuplicateInFolderViewModel _duplicateInFolderViewModel;
    private string _name;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private ObservableCollection<DuplicateCaseItemViewModel> _files;

    public ObservableCollection<DuplicateCaseItemViewModel> Files
    {
        get => _files;
        set => this.RaiseAndSetIfChanged(ref _files, value);
    }

    private DuplicateCaseItemViewModel? _keeper;

    public DuplicateCaseItemViewModel? Keeper
    {
        get => _keeper;
        private set => this.RaiseAndSetIfChanged(ref _keeper, value);
    }

    public long ReclaimableSize
    {
        get
        {
            return Files
                .Where(file => !ReferenceEquals(file, Keeper) && !file.IsDeleted)
                .Sum(file => file.FileSize);
        }
    }

    public string ReclaimableSizeText => $"Reclaimable: {ReclaimableSize:N0} bytes";

    public string KeeperText => Keeper is null ? "Keep: none" : $"Keep: {Keeper.FilePath}";

    public DuplicateCaseViewModel(DuplicateInFolderViewModel duplicateInFolderViewModel,
        IEnumerable<DuplicateFile> files, Trash trash, FileManagerHandler fileManagerHandler,
        IDeleteConfirmation deleteConfirmation)
    {
        _duplicateInFolderViewModel = duplicateInFolderViewModel;
        _name = String.Join(" - ", files.Select(file => Path.GetFileName(file.Path)).Distinct(StringComparer.OrdinalIgnoreCase));
        _files = new ObservableCollection<DuplicateCaseItemViewModel>(files.Select(file =>
            new DuplicateCaseItemViewModel(this, file, fileManagerHandler, trash, deleteConfirmation)));

        foreach (DuplicateCaseItemViewModel file in _files)
        {
            file.PropertyChanged += OnFilePropertyChanged;
        }

        SetKeeper(_files.FirstOrDefault());
    }

    public void SetKeeper(DuplicateCaseItemViewModel? keeper)
    {
        if (keeper is { IsDeleted: true })
        {
            return;
        }

        foreach (DuplicateCaseItemViewModel file in Files)
        {
            file.ApplyKeeper(ReferenceEquals(file, keeper));
        }

        Keeper = keeper;
        this.RaisePropertyChanged(nameof(KeeperText));
        RaiseReclaimableSizeChanged();
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DuplicateCaseItemViewModel.IsDeleted) &&
            sender is DuplicateCaseItemViewModel { IsDeleted: true } file &&
            ReferenceEquals(file, Keeper))
        {
            SetKeeper(Files.FirstOrDefault(candidate => !candidate.IsDeleted));
        }

        RaiseReclaimableSizeChanged();
    }

    private void RaiseReclaimableSizeChanged()
    {
        this.RaisePropertyChanged(nameof(ReclaimableSize));
        this.RaisePropertyChanged(nameof(ReclaimableSizeText));
    }

    public void Remove(DuplicateCaseItemViewModel duplicateCaseItem)
    {
        Files.Remove(duplicateCaseItem);
        if (Files.Count <= 1)
        {
            _duplicateInFolderViewModel.Remove(this);
        }
    }
}