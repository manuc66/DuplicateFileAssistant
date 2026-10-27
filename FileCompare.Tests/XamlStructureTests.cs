namespace FileCompare.Tests;

[TestFixture]
public class XamlStructureTests
{
    [TestCase("DuplicateContentInFolderUserControl.axaml")]
    [TestCase("DuplicateNameInFolderUserControl.axaml")]
    [TestCase("SimilarImageUserControl.axaml")]
    public void DuplicateFileListsUseAVirtualizingControl(string fileName)
    {
        string viewsDirectory = Path.Combine(FindRepositoryRoot(), "DuplicateAssistant", "Views");
        string xaml = File.ReadAllText(Path.Combine(viewsDirectory, fileName));

        Assert.Multiple(() =>
        {
            Assert.That(xaml, Does.Not.Contain("<ItemsControl"), "Use a virtualizing list for file items.");
            Assert.That(xaml, Does.Contain("<ListBox"), "The file list should use a ListBox.");
        });
    }

    [TestCase("DeleteIfElsewhereUserControl.axaml")]
    [TestCase("DuplicateContentInFolderUserControl.axaml")]
    [TestCase("DuplicateNameInFolderUserControl.axaml")]
    [TestCase("SimilarImageUserControl.axaml")]
    [TestCase("MultiFolderDuplicateUserControl.axaml")]
    [TestCase("MergeToDestinationUserControl.axaml")]
    [TestCase("EmptyFolderUserControl.axaml")]
    [TestCase("FileListUserControl.axaml")]
    [TestCase("InvalidLinkUserControl.axaml")]
    [TestCase("TrashUserControl.axaml")]
    public void TabsWithLongOperationsUseTheSharedOperationBar(string fileName)
    {
        string viewsDirectory = Path.Combine(FindRepositoryRoot(), "DuplicateAssistant", "Views");
        string xaml = File.ReadAllText(Path.Combine(viewsDirectory, fileName));

        Assert.Multiple(() =>
        {
            Assert.That(xaml, Does.Contain("<controls:OperationBar"),
                "Long operations must show progress, percentage, pause and stop.");
            Assert.That(xaml, Does.Not.Contain("StopSearchCommand"));
        });
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "DuplicateAssistant", "Views")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
