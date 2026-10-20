namespace DuplicateAssistant.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public DuplicateContentInFolderViewModel DuplicateContentInFolderViewModel { get; }
    public DuplicateNameInFolderViewModel DuplicateNameInFolderViewModel { get; }
    public EmptyFolderViewModel EmptyFolderViewModel { get; }
    public DeleteIfElsewhereViewModel DeleteIfElsewhereViewModel { get; }

    public MainWindowViewModel(DuplicateContentInFolderViewModel duplicateContentInFolderViewModel,
        DuplicateNameInFolderViewModel duplicateNameInFolderViewModel,
        EmptyFolderViewModel emptyFolderViewModel,
        DeleteIfElsewhereViewModel deleteIfElsewhereViewModel)
    {
        DuplicateContentInFolderViewModel = duplicateContentInFolderViewModel;
        DuplicateNameInFolderViewModel = duplicateNameInFolderViewModel;
        EmptyFolderViewModel = emptyFolderViewModel;
        DeleteIfElsewhereViewModel = deleteIfElsewhereViewModel;
    }
}