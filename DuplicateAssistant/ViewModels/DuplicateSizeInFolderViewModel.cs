using System.Collections.Generic;
using System.IO;
using System.Threading;
using DuplicateAssistant.Business;
using FileCompare;

namespace DuplicateAssistant.ViewModels;

public class DuplicateSizeInFolderViewModel : DuplicateInFolderViewModel
{
    public DuplicateSizeInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler,
        IDeleteConfirmation deleteConfirmation, IFolderPicker folderPicker,
        LastFolderStore? lastFolderStore = null) :
        base(trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker, lastFolderStore)
    {
        ShowHashOptions = false;
    }

    protected override Dictionary<HashKey, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        using ControlWriter textDisplayProgress = new(this);
        FileSearchFilter searchFilter = CreateSearchFilter();

        return SizeDuplicateFinder.Find(SearchPath, searchOption, searchFilter, textDisplayProgress, UpdateProgress, ct,
            path => !IsInTrash(path));
    }
}
