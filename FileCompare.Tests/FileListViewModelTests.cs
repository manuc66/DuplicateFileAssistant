using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class FileListViewModelTests
{
    [AvaloniaTest]
    public async Task BigFilesRespectTheExtensionFilter()
    {
        using TempDirectory temp = new();
        string bin = temp.CreateFile("large.bin", new string('a', 100 * 1024));
        temp.CreateFile("large.tmp", new string('b', 100 * 1024));

        FileListViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            FileListMode.BigFiles,
            temp.Root);
        viewModel.ExtensionSelection.AddExtension("bin");

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.Files.Select(file => file.Path), Is.EquivalentTo(new[] { bin }));
    }

    [AvaloniaTest]
    public async Task TemporaryFilesRespectMinimumAndMaximumSize()
    {
        using TempDirectory temp = new();
        temp.CreateFile("small.tmp", new string('a', 1024));
        string medium = temp.CreateFile("medium.tmp", new string('b', 10 * 1024));
        temp.CreateFile("large.tmp", new string('c', 100 * 1024));

        FileListViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            FileListMode.TemporaryFiles,
            temp.Root)
        {
            MinimumSizeText = "5 KB",
            MaximumSizeText = "50 KB"
        };

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.Files.Select(file => file.Path), Is.EquivalentTo(new[] { medium }));
    }

    [AvaloniaTest]
    public async Task BigFilesRespectMinimumAndMaximumSize()
    {
        using TempDirectory temp = new();
        temp.CreateFile("small.txt", new string('a', 1024));
        string medium = temp.CreateFile("medium.txt", new string('b', 10 * 1024));
        temp.CreateFile("large.txt", new string('c', 100 * 1024));

        FileListViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            FileListMode.BigFiles,
            temp.Root)
        {
            MinimumSizeText = "5 KB",
            MaximumSizeText = "50 KB"
        };

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.Files.Select(file => file.Path), Is.EquivalentTo(new[] { medium }));
    }

    [AvaloniaTest]
    public async Task EmptyFilesCanBeScannedAndDeleted()
    {
        using TempDirectory temp = new();
        string empty = temp.CreateFile("empty.txt", string.Empty);
        temp.CreateFile("content.txt", "content");

        Trash trash = new(Path.Combine(temp.Root, "trash"));
        FileListViewModel viewModel = new(
            trash,
            new FakeDeleteConfirmation { Result = true },
            new FakeFolderPicker(),
            FileListMode.EmptyFiles,
            temp.Root);

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.Files.Select(file => file.Path), Is.EquivalentTo(new[] { empty }));

        viewModel.SelectedFile = viewModel.Files[0];
        await viewModel.DeleteSelectedCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(empty), Is.False);
            Assert.That(viewModel.Files, Is.Empty);
            Assert.That(trash.GetEntries(), Has.Count.EqualTo(1));
        });
    }

    [AvaloniaTest]
    public async Task TrashFolderIsExcludedFromFileLists()
    {
        using TempDirectory temp = new();
        string normal = temp.CreateFile("empty.txt", string.Empty);
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        File.WriteAllText(Path.Combine(trash.TrashFolder, "ghost.txt"), string.Empty);

        FileListViewModel viewModel = new(
            trash,
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            FileListMode.EmptyFiles,
            temp.Root);

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.Files.Select(file => file.Path), Is.EquivalentTo(new[] { normal }));
    }

    [AvaloniaTest]
    public async Task FailedFilesRemainVisibleAfterDeletion()
    {
        using TempDirectory temp = new();
        string first = temp.CreateFile("a.txt", string.Empty);
        string second = temp.CreateFile("b.txt", string.Empty);
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        FileListViewModel viewModel = new(
            trash,
            new FakeDeleteConfirmation { Result = true },
            new FakeFolderPicker(),
            FileListMode.EmptyFiles,
            temp.Root);

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();
        File.Delete(second);
        await viewModel.DeleteAllCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(first), Is.False);
            Assert.That(viewModel.Files.Select(file => file.Path), Is.EquivalentTo(new[] { second }));
        });
    }

    [AvaloniaTest]
    public async Task BigFilesAreLimitedAndOrdered()
    {
        using TempDirectory temp = new();
        temp.CreateFile("small.txt", "1");
        string medium = temp.CreateFile("medium.txt", "12345");
        string large = temp.CreateFile("large.txt", "1234567890");

        FileListViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            FileListMode.BigFiles,
            temp.Root)
        {
            MaximumFiles = 2
        };

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.Files.Select(file => file.Path), Is.EqualTo(new[] { large, medium }));
    }

    [TestCase(FileListMode.EmptyFiles, false, false, false)]
    [TestCase(FileListMode.BigFiles, true, true, true)]
    [TestCase(FileListMode.TemporaryFiles, true, false, false)]
    public void FilterVisibilityMatchesTheMode(FileListMode mode, bool sizeFilters, bool extensionFilter,
        bool maximumFiles)
    {
        using TempDirectory temp = new();
        FileListViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            mode,
            temp.Root);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowSizeFilters, Is.EqualTo(sizeFilters));
            Assert.That(viewModel.ShowExtensionFilter, Is.EqualTo(extensionFilter));
            Assert.That(viewModel.ShowMaximumFiles, Is.EqualTo(maximumFiles));
        });
    }

    [AvaloniaTest]
    public async Task AnInvalidSizeIsReportedWithoutScanning()
    {
        using TempDirectory temp = new();
        temp.CreateFile("large.txt", new string('a', 1024));
        FileListViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            FileListMode.BigFiles,
            temp.Root)
        {
            MinimumSizeText = "not-a-size"
        };

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Files, Is.Empty);
            Assert.That(viewModel.Status, Does.Contain("Minimum size"));
        });
    }

    [AvaloniaTest]
    public async Task AMinimumLargerThanTheMaximumIsReported()
    {
        using TempDirectory temp = new();
        temp.CreateFile("large.txt", new string('a', 1024));
        FileListViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            FileListMode.BigFiles,
            temp.Root)
        {
            MinimumSizeText = "10 KB",
            MaximumSizeText = "1 KB"
        };

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Files, Is.Empty);
            Assert.That(viewModel.Status, Does.Contain("must not be greater"));
        });
    }

    [AvaloniaTest]
    public async Task AMissingFolderIsReported()
    {
        using TempDirectory temp = new();
        FileListViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            FileListMode.EmptyFiles,
            Path.Combine(temp.Root, "missing"));

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.Status, Does.Contain("existing folder"));
    }

    [AvaloniaTest]
    public async Task CancellingTheConfirmationLeavesFilesInPlace()
    {
        using TempDirectory temp = new();
        string empty = temp.CreateFile("empty.txt", string.Empty);
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        FileListViewModel viewModel = new(
            trash,
            new FakeDeleteConfirmation { Result = false },
            new FakeFolderPicker(),
            FileListMode.EmptyFiles,
            temp.Root);

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();
        viewModel.SelectedFile = viewModel.Files[0];
        await viewModel.DeleteSelectedCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(empty), Is.True);
            Assert.That(viewModel.Files, Has.Count.EqualTo(1));
            Assert.That(trash.GetEntries(), Is.Empty);
        });
    }

    [AvaloniaTest]
    public async Task ScanningRemembersTheLastFolder()
    {
        using TempDirectory temp = new();
        string defaultFolder = Path.Combine(temp.Root, "default");
        string searchFolder = Path.Combine(temp.Root, "search");
        Directory.CreateDirectory(defaultFolder);
        Directory.CreateDirectory(searchFolder);
        LastFolderStore store = new(
            Path.Combine(temp.Root, "last-folder.json"),
            new LastFolderSettings(defaultFolder));
        FileListViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            FileListMode.EmptyFiles,
            searchFolder,
            store);

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(store.Current.LastFolder, Is.EqualTo(searchFolder));
    }

    [AvaloniaTest]
    public async Task DryRunDoesNotDeleteFiles()
    {
        using TempDirectory temp = new();
        string empty = temp.CreateFile("empty.txt", string.Empty);
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        FileListViewModel viewModel = new(
            trash,
            new FakeDeleteConfirmation { Result = true },
            new FakeFolderPicker(),
            FileListMode.EmptyFiles,
            temp.Root)
        {
            DryRun = true
        };

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();
        viewModel.SelectedFile = viewModel.Files[0];
        await viewModel.DeleteSelectedCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(empty), Is.True);
            Assert.That(trash.GetEntries(), Is.Empty);
            Assert.That(viewModel.Status, Does.Contain("Dry run"));
        });
    }
}
