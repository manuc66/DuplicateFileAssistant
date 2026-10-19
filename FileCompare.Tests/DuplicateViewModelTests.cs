using DuplicateAssistant.Business;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class DuplicateViewModelTests
{
    [Test]
    public void RemovingFileFromLastDuplicateCase_DoesNotThrow()
    {
        using TempDirectory temp = new();
        string trashFolder = Path.Combine(temp.Root, "trash");

        TestDuplicateInFolderViewModel viewModel = new(
            new Trash(trashFolder),
            temp.Root,
            new FileManagerHandler());

        HashSet<string> files = new()
        {
            Path.Combine(temp.Root, "first.jpg"),
            Path.Combine(temp.Root, "second.jpg")
        };

        DuplicateCaseViewModel duplicateCase = new(
            viewModel,
            files,
            new Trash(trashFolder),
            new FileManagerHandler());

        viewModel.DuplicateCaseItems.Add(duplicateCase);

        Assert.That(() => duplicateCase.Remove(duplicateCase.Files[0]), Throws.Nothing);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateCaseItems, Is.Empty);
            Assert.That(viewModel.DuplicateCase, Is.Null);
        });
    }

    private sealed class TestDuplicateInFolderViewModel : DuplicateInFolderViewModel
    {
        public TestDuplicateInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler)
            : base(trash, searchPath, fileManagerHandler)
        {
        }

        protected override Dictionary<string, HashSet<string>> SearchFunction(CancellationToken ct)
        {
            return new Dictionary<string, HashSet<string>>();
        }
    }
}
