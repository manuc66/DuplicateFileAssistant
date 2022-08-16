namespace DuplicateAssistant;

public class MainWindowViewModel : ViewModelBase
{
    public DuplicateInFolderViewModel DuplicateInFolderViewModel { get; }

    public MainWindowViewModel(DuplicateInFolderViewModel duplicateInFolderViewModel)
    {
        DuplicateInFolderViewModel = duplicateInFolderViewModel;
    }
}