namespace DuplicateAssistant.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public DuplicateContentInFolderViewModel DuplicateContentInFolderViewModel { get; }
    public DuplicateNameInFolderViewModel DuplicateNameInFolderViewModel { get; }
    public EmptyFolderViewModel EmptyFolderViewModel { get; }
    public DeleteIfElsewhereViewModel DeleteIfElsewhereViewModel { get; }
    public MergeToDestinationViewModel MergeToDestinationViewModel { get; }
    public TrashViewModel TrashViewModel { get; }
    public FileListViewModel EmptyFileViewModel { get; }
    public FileListViewModel BigFileViewModel { get; }
    public FileListViewModel TemporaryFileViewModel { get; }
    public InvalidLinkViewModel InvalidLinkViewModel { get; }

    public MainWindowViewModel(DuplicateContentInFolderViewModel duplicateContentInFolderViewModel,
        DuplicateNameInFolderViewModel duplicateNameInFolderViewModel,
        EmptyFolderViewModel emptyFolderViewModel,
        DeleteIfElsewhereViewModel deleteIfElsewhereViewModel,
        MergeToDestinationViewModel mergeToDestinationViewModel,
        TrashViewModel trashViewModel,
        FileListViewModel emptyFileViewModel,
        FileListViewModel bigFileViewModel,
        FileListViewModel temporaryFileViewModel,
        InvalidLinkViewModel invalidLinkViewModel)
    {
        DuplicateContentInFolderViewModel = duplicateContentInFolderViewModel;
        DuplicateNameInFolderViewModel = duplicateNameInFolderViewModel;
        EmptyFolderViewModel = emptyFolderViewModel;
        DeleteIfElsewhereViewModel = deleteIfElsewhereViewModel;
        MergeToDestinationViewModel = mergeToDestinationViewModel;
        TrashViewModel = trashViewModel;
        EmptyFileViewModel = emptyFileViewModel;
        BigFileViewModel = bigFileViewModel;
        TemporaryFileViewModel = temporaryFileViewModel;
        InvalidLinkViewModel = invalidLinkViewModel;
    }
}