using System.Collections.Generic;
using System.IO;
using System.Threading;
using DuplicateAssistant.Business;
using FileCompare;

namespace DuplicateAssistant.ViewModels;

public class DuplicateNameInFolderViewModel : DuplicateInFolderViewModel
{
    public DuplicateNameInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler,
        IDeleteConfirmation deleteConfirmation, IFolderPicker folderPicker) : base(
        trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker)
    {
    }

    protected override Dictionary<HashKey, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        using ControlWriter textDisplayProgress = new(this);
        FileSearchFilter searchFilter = CreateSearchFilter();
        return DupProvider.FindDuplicateByFileName(SearchPath, searchOption, searchFilter.MatchesFile, textDisplayProgress,
            UpdateProgress, ct, path => searchFilter.MatchesPath(path) && !IsInTrash(path));
    }
}