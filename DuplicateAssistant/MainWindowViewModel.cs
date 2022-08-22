namespace DuplicateAssistant;

public class MainWindowViewModel : ViewModelBase
{
    public DuplicateInFolderViewModel DuplicateInFolderViewModel { get; }
    public DuplicateNameInFolderViewModel DuplicateNameInFolderViewModel { get; }

    public MainWindowViewModel(DuplicateInFolderViewModel duplicateInFolderViewModel, DuplicateNameInFolderViewModel duplicateNameInFolderViewModel)
    {
        DuplicateInFolderViewModel = duplicateInFolderViewModel;
        DuplicateNameInFolderViewModel = duplicateNameInFolderViewModel;
    }
}