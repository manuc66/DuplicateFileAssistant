using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using DuplicateAssistant.Business;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly TrashSettingsStore _settingsStore;
    private readonly ModelSettingsStore? _modelSettingsStore;
    private readonly IFolderPicker _folderPicker;
    private TrashMode _mode;
    private string _trashFolder;
    private string _modelsFolder;

    public SettingsViewModel(TrashSettingsStore settingsStore, IFolderPicker folderPicker,
        ModelSettingsStore? modelSettingsStore = null)
    {
        _settingsStore = settingsStore;
        _modelSettingsStore = modelSettingsStore;
        _folderPicker = folderPicker;
        _mode = settingsStore.Current.Mode;
        _trashFolder = settingsStore.Current.TrashFolder;
        _modelsFolder = modelSettingsStore?.Current.ModelsFolder ?? CachePaths.ModelsDirectory;

        SaveCommand = ReactiveCommand.Create(Save);
        BrowseFolderCommand = ReactiveCommand.CreateFromTask(BrowseFolderAsync);
        BrowseModelsFolderCommand = ReactiveCommand.CreateFromTask(BrowseModelsFolderAsync);
    }

    public bool UseApplicationTrash
    {
        get => _mode == TrashMode.Application;
        set
        {
            if (value)
            {
                SetMode(TrashMode.Application);
            }
        }
    }

    public bool UseSystemTrash
    {
        get => _mode == TrashMode.System;
        set
        {
            if (value)
            {
                SetMode(TrashMode.System);
            }
        }
    }

    public bool IsApplicationTrash => _mode == TrashMode.Application;

    public string TrashFolder
    {
        get => _trashFolder;
        set
        {
            this.RaiseAndSetIfChanged(ref _trashFolder, value);
            this.RaisePropertyChanged(nameof(LocationDescription));
        }
    }

    public string SystemTrashName => OperatingSystem.IsWindows()
        ? "Windows Recycle Bin"
        : OperatingSystem.IsMacOS()
            ? "Finder Trash"
            : "freedesktop system trash";

    public string LocationDescription => IsApplicationTrash
        ? $"Files are moved to {TrashFolder}. The folder is excluded from scans. Files on another volume are copied to it."
        : $"Files are moved to the {SystemTrashName}. The application still records its own entries so they can be restored from the Trash tab.";

    public string ModelsFolder
    {
        get => _modelsFolder;
        set
        {
            this.RaiseAndSetIfChanged(ref _modelsFolder, value);
            this.RaisePropertyChanged(nameof(ModelsFolderDescription));
        }
    }

    public string ModelsFolderDescription => $"Models are downloaded to {ModelsFolder}.";

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseModelsFolderCommand { get; }

    private void SetMode(TrashMode mode)
    {
        if (_mode == mode)
        {
            return;
        }

        _mode = mode;
        this.RaisePropertyChanged(nameof(UseApplicationTrash));
        this.RaisePropertyChanged(nameof(UseSystemTrash));
        this.RaisePropertyChanged(nameof(IsApplicationTrash));
        this.RaisePropertyChanged(nameof(LocationDescription));
    }

    private async Task BrowseFolderAsync()
    {
        string? selected = await _folderPicker.PickFolderAsync(TrashFolder);

        if (selected != null)
        {
            TrashFolder = selected;
        }
    }

    private async Task BrowseModelsFolderAsync()
    {
        string? selected = await _folderPicker.PickFolderAsync(ModelsFolder);

        if (selected != null)
        {
            ModelsFolder = selected;
        }
    }

    private void Save()
    {
        try
        {
            string folder = TrashSettingsStore.NormalizeFolder(TrashFolder);

            if (_mode == TrashMode.Application)
            {
                EnsureWritableDirectory(folder);
            }

            string? modelsFolder = null;

            if (_modelSettingsStore is not null)
            {
                modelsFolder = ModelSettingsStore.NormalizeFolder(ModelsFolder);
                EnsureWritableDirectory(modelsFolder);
            }

            _settingsStore.Save(new TrashSettings(_mode, folder));
            TrashFolder = folder;

            if (_modelSettingsStore is not null && modelsFolder is not null)
            {
                _modelSettingsStore.Save(new ModelSettings(modelsFolder));
                ModelsFolder = modelsFolder;
            }

            Status = IsApplicationTrash
                ? $"Saved. New deletions go to {folder}."
                : $"Saved. New deletions use the {SystemTrashName}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                       NotSupportedException or PathTooLongException)
        {
            Status = ex.Message;
        }
    }

    private static void EnsureWritableDirectory(string folder)
    {
        Directory.CreateDirectory(folder);

        string probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}.tmp");
        using (File.Create(probe))
        {
        }

        File.Delete(probe);
    }
}
