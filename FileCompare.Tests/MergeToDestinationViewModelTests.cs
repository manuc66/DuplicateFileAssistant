using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class MergeToDestinationViewModelTests
{
    [AvaloniaTest]
    public async Task DryRunDoesNotMoveOrTrashAnything()
    {
        using TempDirectory temp = new();
        string source = Path.Combine(temp.Root, "source");
        string destination = Path.Combine(temp.Root, "destination");
        string sourceFile = temp.CreateFile(Path.Combine("source", "move.txt"), "move");
        Directory.CreateDirectory(destination);

        Trash trash = new(Path.Combine(temp.Root, "trash"));
        MergeToDestinationViewModel viewModel = new(
            trash,
            source,
            new FakeDeleteConfirmation { Result = true },
            new FakeFolderPicker())
        {
            DestinationPath = destination,
            DryRun = true
        };

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();
        await viewModel.MergeCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(sourceFile), Is.True);
            Assert.That(File.Exists(Path.Combine(destination, "move.txt")), Is.False);
            Assert.That(trash.GetEntries(), Is.Empty);
            Assert.That(viewModel.Status, Does.Contain("Dry run"));
        });
    }

    [AvaloniaTest]
    public async Task ScanAndMergeMovesFilesAndLeavesConflictsUntouched()
    {
        using TempDirectory temp = new();
        string source = Path.Combine(temp.Root, "source");
        string destination = Path.Combine(temp.Root, "destination");
        string moveSource = temp.CreateFile(Path.Combine("source", "move.txt"), "move");
        string duplicateSource = temp.CreateFile(Path.Combine("source", "duplicate.txt"), "duplicate");
        string conflictSource = temp.CreateFile(Path.Combine("source", "conflict.txt"), "source content");
        temp.CreateFile(Path.Combine("destination", "duplicate.txt"), "duplicate");
        string conflictDestination = temp.CreateFile(Path.Combine("destination", "conflict.txt"), "destination content");

        Trash trash = new(Path.Combine(temp.Root, "trash"));
        FakeDeleteConfirmation confirmation = new() { Result = true };
        MergeToDestinationViewModel viewModel = new(trash, source, confirmation, new FakeFolderPicker())
        {
            DestinationPath = destination
        };

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.MoveCount, Is.EqualTo(1));
            Assert.That(viewModel.TrashCount, Is.EqualTo(1));
            Assert.That(viewModel.ConflictCount, Is.EqualTo(1));
            Assert.That(viewModel.HasPlan, Is.True);
        });

        await viewModel.MergeCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(moveSource), Is.False);
            Assert.That(File.Exists(Path.Combine(destination, "move.txt")), Is.True);
            Assert.That(File.Exists(duplicateSource), Is.False);
            Assert.That(File.Exists(conflictSource), Is.True);
            Assert.That(File.ReadAllText(conflictDestination), Is.EqualTo("destination content"));
            Assert.That(viewModel.Plan, Is.Empty);
            Assert.That(viewModel.Status, Does.Contain("Moved 1"));
        });
    }

    [AvaloniaTest]
    public async Task MergeDoesNotDeleteATrashFolderInsideTheSource()
    {
        using TempDirectory temp = new();
        string source = Directory.CreateDirectory(Path.Combine(temp.Root, "source")).FullName;
        string destination = Directory.CreateDirectory(Path.Combine(temp.Root, "destination")).FullName;
        temp.CreateFile(Path.Combine("source", "move.txt"), "move");
        string trashFolder = Path.Combine(source, ".trash");
        Trash trash = new(trashFolder);
        MergeToDestinationViewModel viewModel = new(
            trash,
            source,
            new FakeDeleteConfirmation { Result = true },
            new FakeFolderPicker())
        {
            DestinationPath = destination
        };

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();
        await viewModel.MergeCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(destination, "move.txt")), Is.True);
            Assert.That(Directory.Exists(trashFolder), Is.True);
        });
    }
}
