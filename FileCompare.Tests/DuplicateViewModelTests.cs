using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.Business;
using DuplicateAssistant.ViewModels;
using ImageCompare;
using SkiaSharp;
using System.Reactive;
using System.Reactive.Linq;

namespace FileCompare.Tests;

[TestFixture]
public class DuplicateViewModelTests
{
    [Test]
    public void SimilarImageDetailsSelectKeeperAndDisableHardlink()
    {
        using TempDirectory temp = new();
        string firstPath = temp.CreateFile("first.jpg", "first");
        string secondPath = temp.CreateFile("second.jpg", "second");
        FileInfo firstInfo = new(firstPath);
        FileInfo secondInfo = new(secondPath);
        FakeDeleteConfirmation confirmation = new();
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        TestDuplicateInFolderViewModel viewModel = new(
            trash,
            temp.Root,
            new FileManagerHandler(),
            confirmation,
            new FakeFolderPicker());

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(firstPath, firstInfo.Length, firstInfo.LastWriteTimeUtc),
            new DuplicateFile(secondPath, secondInfo.Length, secondInfo.LastWriteTimeUtc)
        };

        DuplicateCaseViewModel duplicateCase = new(
            viewModel,
            files,
            trash,
            new FileManagerHandler(),
            confirmation);

        duplicateCase.ApplySimilarImageDetails(
            SimilarImageKind.Semantic,
            maximumDistance: 14,
            minimumSimilarity: 0.94,
            keeperPath: secondPath,
            keeperQuality: 0.8);

        DuplicateCaseItemViewModel first = duplicateCase.Files.First(file => file.FilePath == firstPath);
        DuplicateCaseItemViewModel second = duplicateCase.Files.First(file => file.FilePath == secondPath);

        Assert.Multiple(() =>
        {
            Assert.That(duplicateCase.Kind, Is.EqualTo(SimilarImageKind.Semantic));
            Assert.That(duplicateCase.KindText, Is.EqualTo("Semantic"));
            Assert.That(duplicateCase.Keeper, Is.SameAs(second));
            Assert.That(second.IsKeeper, Is.True);
            Assert.That(first.IsKeeper, Is.False);
            Assert.That(first.CanHardlink, Is.False);
            Assert.That(second.CanHardlink, Is.False);
            Assert.That(duplicateCase.Detail, Does.Contain("min similarity"));
            Assert.That(duplicateCase.Detail, Does.Contain("dHash max 14"));
            Assert.That(duplicateCase.SimilarityText, Does.Contain("Minimum similarity"));
            Assert.That(duplicateCase.KeeperQualityText, Does.Contain("Keeper quality"));
        });
    }

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
    public async Task DeleteFailureIsReportedOnTheItem()
    {
        using TempDirectory temp = new();
        string keeperPath = temp.CreateFile("keeper.jpg", "content");
        string duplicatePath = temp.CreateFile("duplicate.jpg", "content");
        FileInfo keeperInfo = new(keeperPath);
        FileInfo duplicateInfo = new(duplicatePath);

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(keeperPath, keeperInfo.Length, keeperInfo.LastWriteTimeUtc),
            new DuplicateFile(duplicatePath, duplicateInfo.Length, duplicateInfo.LastWriteTimeUtc)
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
        viewModel.DuplicateCaseItems.Add(duplicateCase);

        DuplicateCaseItemViewModel keeper = duplicateCase.Keeper!;
        DuplicateCaseItemViewModel duplicate = duplicateCase.Files.First(file => !ReferenceEquals(file, keeper));
        File.WriteAllText(duplicate.FilePath, "changed content");

        await duplicate.DeleteCommand.Execute(duplicate.FilePath).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.HasError, Is.True);
            Assert.That(duplicate.IsDeleted, Is.False);
            Assert.That(File.Exists(duplicate.FilePath), Is.True);
        });
    }

    [Test]
    public async Task HardlinkReplacesADuplicateWithALinkToTheKeeper()
    {
        using TempDirectory temp = new();
        string keeperPath = temp.CreateFile("keeper.jpg", "content");
        string duplicatePath = temp.CreateFile("duplicate.jpg", "content");
        FileInfo keeperInfo = new(keeperPath);
        FileInfo duplicateInfo = new(duplicatePath);

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(keeperPath, keeperInfo.Length, keeperInfo.LastWriteTimeUtc),
            new DuplicateFile(duplicatePath, duplicateInfo.Length, duplicateInfo.LastWriteTimeUtc)
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
        viewModel.DuplicateCaseItems.Add(duplicateCase);

        DuplicateCaseItemViewModel keeper = duplicateCase.Keeper!;
        DuplicateCaseItemViewModel duplicate = duplicateCase.Files.First(file => !ReferenceEquals(file, keeper));
        await duplicate.HardlinkCommand.Execute(Unit.Default).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(keeperPath), Is.True);
            Assert.That(File.Exists(duplicatePath), Is.True);
            Assert.That(FileIdentity.TryGet(keeperPath, out FileIdentity keeperId), Is.True);
            Assert.That(FileIdentity.TryGet(duplicatePath, out FileIdentity duplicateId), Is.True);
            Assert.That(keeperId, Is.EqualTo(duplicateId));
            Assert.That(trash.GetEntries(), Is.Empty);
            Assert.That(viewModel.DuplicateCaseItems, Is.Empty);
        });
    }

    [Test]
    public async Task AutoRuleSelectsTheOldestKeeperAndDeletesNonKeepers()
    {
        using TempDirectory temp = new();
        string oldestPath = temp.CreateFile("oldest.jpg", "content");
        string newestPath = temp.CreateFile("newest.jpg", "content");
        File.SetLastWriteTimeUtc(oldestPath, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(newestPath, DateTime.UtcNow.AddDays(-1));
        FileInfo oldestInfo = new(oldestPath);
        FileInfo newestInfo = new(newestPath);

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(oldestPath, oldestInfo.Length, oldestInfo.LastWriteTimeUtc),
            new DuplicateFile(newestPath, newestInfo.Length, newestInfo.LastWriteTimeUtc)
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
        viewModel.DuplicateCaseItems.Add(duplicateCase);

        viewModel.AutoDeleteRule = AutoDeleteRule.KeepOldest;
        await viewModel.ApplyRuleCommand.Execute(Unit.Default).FirstAsync();

        Assert.That(duplicateCase.Keeper!.FilePath, Is.EqualTo(oldestPath));

        await viewModel.DeleteNonKeepersCommand.Execute(Unit.Default).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(oldestPath), Is.True);
            Assert.That(File.Exists(newestPath), Is.False);
            Assert.That(trash.GetEntries(), Has.Count.EqualTo(1));
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

    [AvaloniaTest]
    public async Task PreviewsAreLoadedForKeeperAndSelectedFile()
    {
        using TempDirectory temp = new();
        using SKBitmap image = TestImages.CreatePattern(800, 600);
        string keeperPath = temp.CreateFile("keeper.png", TestImages.Encode(image, png: true));
        string otherPath = temp.CreateFile("other.png", TestImages.Encode(image, png: true));

        FakeDeleteConfirmation confirmation = new();
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        TestDuplicateInFolderViewModel viewModel = new(
            trash,
            temp.Root,
            new FileManagerHandler(),
            confirmation,
            new FakeFolderPicker());
        DuplicateCaseViewModel duplicateCase = CreateCase(
            viewModel,
            trash,
            confirmation,
            keeperPath,
            otherPath);

        duplicateCase.ApplySimilarImageDetails(
            SimilarImageKind.Perceptual,
            maximumDistance: 0,
            minimumSimilarity: 1.0,
            keeperPath: keeperPath,
            keeperQuality: 0.5);

        await duplicateCase.LoadPreviewsAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(duplicateCase.KeeperPreview, Is.Not.Null);
            Assert.That(duplicateCase.SelectedPreview, Is.Not.Null);
            Assert.That(duplicateCase.SelectedFile, Is.Not.Null);
            Assert.That(duplicateCase.SelectedFile!.FilePath, Is.EqualTo(otherPath));
        });
    }

    [Test]
    public async Task DeleteSelectedNonKeepersOnlyTouchesTheSelectedGroup()
    {
        using TempDirectory temp = new();
        string keeperA = temp.CreateFile("keeper-a.jpg", "content-a");
        string duplicateA = temp.CreateFile("duplicate-a.jpg", "content-a");
        string keeperB = temp.CreateFile("keeper-b.jpg", "content-b");
        string duplicateB = temp.CreateFile("duplicate-b.jpg", "content-b");

        FakeDeleteConfirmation confirmation = new() { Result = true };
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        TestDuplicateInFolderViewModel viewModel = new(
            trash,
            temp.Root,
            new FileManagerHandler(),
            confirmation,
            new FakeFolderPicker());

        DuplicateCaseViewModel caseA = CreateCase(viewModel, trash, confirmation, keeperA, duplicateA);
        DuplicateCaseViewModel caseB = CreateCase(viewModel, trash, confirmation, keeperB, duplicateB);
        viewModel.DuplicateCaseItems.Add(caseA);
        viewModel.DuplicateCaseItems.Add(caseB);
        viewModel.DuplicateCase = caseA;

        await viewModel.DeleteSelectedNonKeepersCommand.Execute(Unit.Default).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(keeperA), Is.True);
            Assert.That(File.Exists(duplicateA), Is.False);
            Assert.That(File.Exists(keeperB), Is.True);
            Assert.That(File.Exists(duplicateB), Is.True);
            Assert.That(trash.GetEntries(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task DeleteSelectedNonKeepersHonorsDryRun()
    {
        using TempDirectory temp = new();
        string keeper = temp.CreateFile("keeper.jpg", "content");
        string duplicate = temp.CreateFile("duplicate.jpg", "content");

        FakeDeleteConfirmation confirmation = new() { Result = true };
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        TestDuplicateInFolderViewModel viewModel = new(
            trash,
            temp.Root,
            new FileManagerHandler(),
            confirmation,
            new FakeFolderPicker());

        DuplicateCaseViewModel duplicateCase = CreateCase(
            viewModel,
            trash,
            confirmation,
            keeper,
            duplicate);
        viewModel.DuplicateCaseItems.Add(duplicateCase);
        viewModel.DuplicateCase = duplicateCase;
        viewModel.DryRun = true;

        await viewModel.DeleteSelectedNonKeepersCommand.Execute(Unit.Default).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(duplicate), Is.True);
            Assert.That(trash.GetEntries(), Is.Empty);
            Assert.That(viewModel.SearchLog, Does.Contain("Dry run"));
        });
    }

    private static DuplicateCaseViewModel CreateCase(TestDuplicateInFolderViewModel viewModel,
        Trash trash, IDeleteConfirmation confirmation, string keeperPath, string duplicatePath)
    {
        FileInfo keeperInfo = new(keeperPath);
        FileInfo duplicateInfo = new(duplicatePath);

        HashSet<DuplicateFile> files = new()
        {
            new DuplicateFile(keeperPath, keeperInfo.Length, keeperInfo.LastWriteTimeUtc),
            new DuplicateFile(duplicatePath, duplicateInfo.Length, duplicateInfo.LastWriteTimeUtc)
        };

        DuplicateCaseViewModel duplicateCase = new(
            viewModel,
            files,
            trash,
            new FileManagerHandler(),
            confirmation);

        duplicateCase.SetKeeper(duplicateCase.Files.First(file => file.FilePath == keeperPath));
        return duplicateCase;
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
