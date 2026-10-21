using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class EmptyFolderViewModelTests
{
    [AvaloniaTest]
    public async Task ScanThenDeleteRemovesReportedEmptyFolders()
    {
        using TempDirectory temp = new();
        string root = temp.Root;
        string empty = Directory.CreateDirectory(Path.Combine(root, "empty")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;
        string leaf = Directory.CreateDirectory(Path.Combine(root, "nested", "leaf")).FullName;
        string occupied = Directory.CreateDirectory(Path.Combine(root, "occupied")).FullName;
        temp.CreateFile(Path.Combine("occupied", "file.txt"), "content");

        FakeDeleteConfirmation confirmation = new() { Result = true };
        Trash trash = new(Path.Combine(temp.Root, "trash"));
        EmptyFolderViewModel viewModel = new(root, confirmation, new FakeFolderPicker(), trash);

        await viewModel.ScanCommand.Execute(Unit.Default).FirstAsync();
        await TestUi.WaitForAsync(
            () => viewModel.EmptyDirectories.Count == 3,
            "the empty folders to appear");

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.EmptyDirectories, Is.EquivalentTo(new[] { empty, leaf, nested }));
            Assert.That(viewModel.HasResults, Is.True);
            Assert.That(viewModel.Status, Does.Contain("3"));
        });

        await viewModel.DeleteCommand.Execute(Unit.Default).FirstAsync();
        await TestUi.WaitForAsync(
            () => viewModel.EmptyDirectories.Count == 0,
            "the empty folder list to be cleared");

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(empty), Is.False);
            Assert.That(Directory.Exists(leaf), Is.False);
            Assert.That(Directory.Exists(nested), Is.False);
            Assert.That(Directory.Exists(occupied), Is.True);
            Assert.That(viewModel.EmptyDirectories, Is.Empty);
            Assert.That(confirmation.LastPath, Is.EqualTo(root));
        });
    }
}
