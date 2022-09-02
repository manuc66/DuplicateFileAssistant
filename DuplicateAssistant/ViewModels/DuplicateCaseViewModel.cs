using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using DuplicateAssistant.Business;
using FileCompare;
using ReactiveUI;

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

    public DuplicateCaseViewModel(DuplicateInFolderViewModel duplicateInFolderViewModel, HashSet<string> argValue, Trash trash, FileManagerHandler fileManagerHandler)
    {
        _duplicateInFolderViewModel = duplicateInFolderViewModel;
        _name = String.Join(" - ", argValue.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase));
        _files = new ObservableCollection<DuplicateCaseItemViewModel>(argValue.Select(x => new DuplicateCaseItemViewModel(this, x, fileManagerHandler, trash)));
    }

    public void Remove(DuplicateCaseItemViewModel duplicateCaseItem)
    {
        Files.Remove(duplicateCaseItem);
        if (Files.Count == 1)
        {
            _duplicateInFolderViewModel.Remove(this);
        }
    }
}