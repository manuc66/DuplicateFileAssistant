using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class InvalidLinkViewModelTests
{
    [AvaloniaTest]
    public async Task ScansAndMovesBrokenLinksToTheTrash()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Symbolic link creation is tested on Linux.");
        }

        using TempDirectory temp = new();
        string link = Path.Combine(temp.Root, "broken-link");
        File.CreateSymbolicLink(link, Path.Combine(temp.Root, "missing.txt"));

        Trash trash = new(Path.Combine(temp.Root, "trash"));
        InvalidLinkViewModel viewModel = new(
            trash,
            new FakeDeleteConfirmation { Result = true },
            new FakeFolderPicker(),
            temp.Root);

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.Links, Has.Count.EqualTo(1));

        viewModel.SelectedLink = viewModel.Links[0];
        await viewModel.DeleteSelectedCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(new FileInfo(link).LinkTarget, Is.Null);
            Assert.That(viewModel.Links, Is.Empty);
            Assert.That(trash.GetEntries().Single().IsSymbolicLink, Is.True);
        });
    }

    [AvaloniaTest]
    public async Task FailedLinksRemainVisibleAfterDeletion()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Symbolic link creation is tested on Linux.");
        }

        using TempDirectory temp = new();
        string first = Path.Combine(temp.Root, "first-link");
        string second = Path.Combine(temp.Root, "second-link");
        File.CreateSymbolicLink(first, Path.Combine(temp.Root, "missing-1"));
        File.CreateSymbolicLink(second, Path.Combine(temp.Root, "missing-2"));

        Trash trash = new(Path.Combine(temp.Root, "trash"));
        InvalidLinkViewModel viewModel = new(
            trash,
            new FakeDeleteConfirmation { Result = true },
            new FakeFolderPicker(),
            temp.Root);

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();
        File.Delete(second);
        await viewModel.DeleteAllCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.Links.Select(link => link.Path), Is.EquivalentTo(new[] { second }));
    }
}
