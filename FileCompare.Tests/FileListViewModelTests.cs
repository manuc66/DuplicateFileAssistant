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
