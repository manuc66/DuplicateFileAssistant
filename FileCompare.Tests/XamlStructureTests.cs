namespace FileCompare.Tests;

[TestFixture]
public class XamlStructureTests
{
    [TestCase("DuplicateContentInFolderUserControl.axaml")]
    [TestCase("DuplicateNameInFolderUserControl.axaml")]
    public void DuplicateFileListsUseAVirtualizingControl(string fileName)
    {
        string viewsDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DuplicateAssistant", "Views"));
        string xaml = File.ReadAllText(Path.Combine(viewsDirectory, fileName));

        Assert.Multiple(() =>
        {
            Assert.That(xaml, Does.Not.Contain("<ItemsControl"), "Use a virtualizing list for file items.");
            Assert.That(xaml, Does.Contain("<ListBox"), "The file list should use a ListBox.");
        });
    }
}
