using DuplicateAssistant.Business;
using DuplicateAssistant.ViewModels;
using System.Reactive;
using System.Reactive.Linq;

namespace FileCompare.Tests;

[TestFixture]
public class DuplicateViewModelTests
{
    [Test]
    public void DuplicateCase_DefaultsToFirstKeeperAndUpdatesReclaimableSize()
    {
        using TempDirectory temp = new();
        FakeDeleteConfirmation confirmation = new();
        TestDuplicateInFolderViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            temp.Root,
            new FileManagerHandler(),
            confirmation,
            new FakeFolderPicker());

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(Path.Combine(temp.Root, "first.jpg"), 100, DateTime.MinValue),
            new DuplicateFile(Path.Combine(temp.Root, "second.jpg"), 200, DateTime.MinValue)
        };

        DuplicateCaseViewModel duplicateCase = new(
            viewModel,
            files,
            new Trash(Path.Combine(temp.Root, "trash")),
            new FileManagerHandler(),
            confirmation);

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
    public async Task KeeperCannotBeDeleted()
    {
        using TempDirectory temp = new();
        string firstPath = temp.CreateFile("first.jpg", "content");
        string secondPath = temp.CreateFile("second.jpg", "content");
        FileInfo firstInfo = new(firstPath);
        FileInfo secondInfo = new(secondPath);

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(firstPath, firstInfo.Length, firstInfo.LastWriteTimeUtc),
            new DuplicateFile(secondPath, secondInfo.Length, secondInfo.LastWriteTimeUtc)
        };

        FakeDeleteConfirmation confirmation = new() { Result = true };
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        TestDuplicateInFolderViewModel viewModel = new(
            trash,
            temp.Root,
            new FileManagerHandler(),
            confirmation,
            new FakeFolderPicker());
        DuplicateCaseViewModel duplicateCase = new(
            viewModel,
            files,
            trash,
            new FileManagerHandler(),
            confirmation);

        DuplicateCaseItemViewModel keeper = duplicateCase.Keeper!;

        await keeper.DeleteCommand.Execute(keeper.FilePath).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(keeper.CanDelete, Is.False);
            Assert.That(keeper.IsDeleted, Is.False);
            Assert.That(File.Exists(keeper.FilePath), Is.True);
            Assert.That(confirmation.LastPath, Is.Null);
        });
    }

    [Test]
    public async Task DeleteAndRestore_MovesFileAndCanUndo()
    {
        using TempDirectory temp = new();
        string firstPath = temp.CreateFile("first.jpg", "content");
        string secondPath = temp.CreateFile("second.jpg", "content");
        FileInfo firstInfo = new(firstPath);
        FileInfo secondInfo = new(secondPath);

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(firstPath, firstInfo.Length, firstInfo.LastWriteTimeUtc),
            new DuplicateFile(secondPath, secondInfo.Length, secondInfo.LastWriteTimeUtc)
        };

        FakeDeleteConfirmation confirmation = new() { Result = false };
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        TestDuplicateInFolderViewModel viewModel = new(
            trash,
            temp.Root,
            new FileManagerHandler(),
            confirmation,
            new FakeFolderPicker());

        DuplicateCaseViewModel duplicateCase = new(
            viewModel,
            files,
            trash,
            new FileManagerHandler(),
            confirmation);

        duplicateCase.SetKeeper(duplicateCase.Files[1]);
        DuplicateCaseItemViewModel item = duplicateCase.Files[0];

        await item.DeleteCommand.Execute(firstPath).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(firstPath), Is.True);
            Assert.That(item.IsDeleted, Is.False);
            Assert.That(confirmation.LastPath, Is.EqualTo(firstPath));
        });

        confirmation.Result = true;
        await item.DeleteCommand.Execute(firstPath).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(firstPath), Is.False);
            Assert.That(item.IsDeleted, Is.True);
        });

        await item.RestoreCommand.Execute(Unit.Default).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(firstPath), Is.True);
            Assert.That(item.IsDeleted, Is.False);
        });
    }

    [Test]
    public void RemovingFileFromLastDuplicateCase_DoesNotThrow()
    {
        using TempDirectory temp = new();
        string trashFolder = Path.Combine(temp.Root, "trash");

        FakeDeleteConfirmation confirmation = new();
        TestDuplicateInFolderViewModel viewModel = new(
            new Trash(trashFolder),
            temp.Root,
            new FileManagerHandler(),
            confirmation,
            new FakeFolderPicker());

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(Path.Combine(temp.Root, "first.jpg"), 0, DateTime.MinValue),
            new DuplicateFile(Path.Combine(temp.Root, "second.jpg"), 0, DateTime.MinValue)
        };

        DuplicateCaseViewModel duplicateCase = new(
            viewModel,
            files,
            new Trash(trashFolder),
            new FileManagerHandler(),
            confirmation);

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
        public TestDuplicateInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler,
            IDeleteConfirmation deleteConfirmation, IFolderPicker folderPicker)
            : base(trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker)
        {
        }

        protected override Dictionary<HashKey, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct)
        {
            return new Dictionary<HashKey, HashSet<DuplicateFile>>();
        }
    }
}
