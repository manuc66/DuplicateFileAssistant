using DuplicateAssistant.Business;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class DuplicateViewModelTests
{
    [Test]
    public void DuplicateCase_DefaultsToFirstKeeperAndUpdatesReclaimableSize()
    {
        using TempDirectory temp = new();
        TestDuplicateInFolderViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            temp.Root,
            new FileManagerHandler());

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(Path.Combine(temp.Root, "first.jpg"), 100, DateTime.MinValue),
            new DuplicateFile(Path.Combine(temp.Root, "second.jpg"), 200, DateTime.MinValue)
        };

        DuplicateCaseViewModel duplicateCase = new(
            viewModel,
            files,
            new Trash(Path.Combine(temp.Root, "trash")),
            new FileManagerHandler());

        DuplicateCaseItemViewModel first = duplicateCase.Files[0];
        DuplicateCaseItemViewModel second = duplicateCase.Files[1];

        Assert.Multiple(() =>
        {
            Assert.That(duplicateCase.Keeper, Is.SameAs(first));
            Assert.That(first.IsKeeper, Is.True);
            Assert.That(second.IsKeeper, Is.False);
            Assert.That(duplicateCase.ReclaimableSize, Is.EqualTo(200));
        });

        duplicateCase.SetKeeper(second);

        Assert.Multiple(() =>
        {
            Assert.That(duplicateCase.Keeper, Is.SameAs(second));
            Assert.That(first.IsKeeper, Is.False);
            Assert.That(second.IsKeeper, Is.True);
            Assert.That(duplicateCase.ReclaimableSize, Is.EqualTo(100));
        });
    }

    [Test]
    public void RemovingFileFromLastDuplicateCase_DoesNotThrow()
    {
        using TempDirectory temp = new();
        string trashFolder = Path.Combine(temp.Root, "trash");

        TestDuplicateInFolderViewModel viewModel = new(
            new Trash(trashFolder),
            temp.Root,
            new FileManagerHandler());

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(Path.Combine(temp.Root, "first.jpg"), 0, DateTime.MinValue),
            new DuplicateFile(Path.Combine(temp.Root, "second.jpg"), 0, DateTime.MinValue)
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

        protected override Dictionary<string, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct)
        {
            return new Dictionary<string, HashSet<DuplicateFile>>();
        }
    }
}
