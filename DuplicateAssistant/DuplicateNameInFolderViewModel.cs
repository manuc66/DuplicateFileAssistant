using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using FileCompare;
using ReactiveUI;

namespace DuplicateAssistant;

public class DuplicateNameInFolderViewModel : DuplicateInFolderViewModel
{
   

    public DuplicateNameInFolderViewModel(Trash trash, string searchPath): base(trash, searchPath)
    {
    }
    
    protected override Dictionary<string, HashSet<string>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return DupProvider.FindDuplicateByFileName(SearchPath, searchOption, new ControlWriter(this),
            progress => { ProgressValue = progress; }, ct);
    }
}