using System.Collections.Generic;
using System.IO;
using System.Threading;
using FileCompare;

namespace DuplicateAssistant;

public class DuplicateContentInFolderViewModel : DuplicateInFolderViewModel
{
    public DuplicateContentInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler) :
        base(trash, searchPath, fileManagerHandler)
    {
    }

    protected override Dictionary<string, HashSet<string>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return DupProvider.FindDuplicateByHash(SearchPath, searchOption, new ControlWriter(this),
            progress => { ProgressValue = progress; }, ct);
    }
}