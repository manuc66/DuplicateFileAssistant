using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class MultiFolderDuplicateViewModelTests
{
    [AvaloniaTest]
    public async Task FindsDuplicatesAcrossAddedFolders()
    {
        using TempDirectory temp = new();
        string firstRoot = Path.Combine(temp.Root, "first");
        string secondRoot = Path.Combine(temp.Root, "second");
        string first = temp.CreateFile(Path.Combine("first", "a.txt"), "same");
        string second = temp.CreateFile(Path.Combine("second", "b.txt"), "same");

        Trash trash = new(Path.Combine(temp.Root, "trash"));
        MultiFolderDuplicateViewModel viewModel = new(
            trash,
            firstRoot,
            new DuplicateAssistant.Business.FileManagerHandler(),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker());

        viewModel.Folders.Add(secondRoot);

        var searchResult = await viewModel.SearchCommand.Execute(Unit.Default).LastAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.That(searchResult, Has.Count.EqualTo(1), viewModel.SearchLog);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateCaseItems, Has.Count.EqualTo(1));
            Assert.That(
                viewModel.DuplicateCaseItems[0].Files.Select(file => file.FilePath),
                Is.EquivalentTo(new[] { first, second }));
        });
    }
}
