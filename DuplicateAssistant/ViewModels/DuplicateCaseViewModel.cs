using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public DuplicateCaseViewModel(DuplicateInFolderViewModel duplicateInFolderViewModel, IEnumerable<DuplicateFile> files, Trash trash, FileManagerHandler fileManagerHandler)
    {
        _duplicateInFolderViewModel = duplicateInFolderViewModel;
        _name = String.Join(" - ", files.Select(file => Path.GetFileName(file.Path)).Distinct(StringComparer.OrdinalIgnoreCase));
        _files = new ObservableCollection<DuplicateCaseItemViewModel>(files.Select(file => new DuplicateCaseItemViewModel(this, file, fileManagerHandler, trash)));
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