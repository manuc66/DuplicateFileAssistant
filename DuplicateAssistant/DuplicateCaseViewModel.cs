using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ReactiveUI;

namespace DuplicateAssistant;

public class DuplicateCaseViewModel : ViewModelBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }
    
    private ObservableCollection<string> _files;
    public ObservableCollection<string> Files
    {
        get => _files;
        set => this.RaiseAndSetIfChanged(ref _files, value);
    }

    public DuplicateCaseViewModel(HashSet<string> argValue)
    {

        _name = String.Join(" - ", argValue.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase));
        _files = new(argValue);
    }
}