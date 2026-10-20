using System.Collections.Generic;
using System.IO;
using System.Threading;
using DuplicateAssistant.Business;
using FileCompare;

namespace DuplicateAssistant.ViewModels;

public class DuplicateNameInFolderViewModel : DuplicateInFolderViewModel
{
    public DuplicateNameInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler) : base(
        trash, searchPath, fileManagerHandler)
    {
    }

    protected override Dictionary<string, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        using ControlWriter textDisplayProgress = new(this);
        return DupProvider.FindDuplicateByFileName(SearchPath, searchOption, _ => true, textDisplayProgress,
            UpdateProgress, ct);
    }
}