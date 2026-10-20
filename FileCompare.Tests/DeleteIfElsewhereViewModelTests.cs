using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class DeleteIfElsewhereViewModelTests
{
    [AvaloniaTest]
    public async Task ScanAndDeleteMovesOnlySourceFilesPresentElsewhere()
    {
        using TempDirectory temp = new();
        string source = Path.Combine(temp.Root, "source");
        string other = Path.Combine(temp.Root, "other");
        string sourceCopy = temp.CreateFile(Path.Combine("source", "a.txt"), "same");
        temp.CreateFile(Path.Combine("source", "unique.txt"), "unique source");
        temp.CreateFile(Path.Combine("other", "b.txt"), "same");
        temp.CreateFile(Path.Combine("other", "unique.txt"), "unique other");

        Trash trash = new(Path.Combine(temp.Root, "trash"));
        FakeDeleteConfirmation confirmation = new() { Result = true };
        DeleteIfElsewhereViewModel viewModel = new(trash, source, confirmation, new FakeFolderPicker())
        {
            OtherPath = other
        };

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Duplicates, Has.Count.EqualTo(1));
            Assert.That(viewModel.Duplicates[0].Source.Path, Is.EqualTo(sourceCopy));
            Assert.That(viewModel.HasResults, Is.True);
        });

        await viewModel.DeleteCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(sourceCopy), Is.False);
            Assert.That(File.Exists(Path.Combine(source, "unique.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(other, "b.txt")), Is.True);
            Assert.That(Directory.GetFiles(trash.TrashFolder), Has.Length.EqualTo(1));
            Assert.That(viewModel.Duplicates, Is.Empty);
        });
    }
}
