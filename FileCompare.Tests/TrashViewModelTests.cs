using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class TrashViewModelTests
{
    [AvaloniaTest]
    public async Task RestoresAnEntryLoadedFromDiskAfterRestart()
    {
        using TempDirectory temp = new();
        string source = temp.CreateFile("photo.jpg", "content");
        string trashFolder = Path.Combine(temp.Root, "trash");

        Trash firstInstance = new(trashFolder);
        await firstInstance.DeleteAsync(source);

        Trash restartedInstance = new(trashFolder);
        FakeDeleteConfirmation confirmation = new() { Result = true };
        TrashViewModel viewModel = new(restartedInstance, confirmation);

        Assert.That(viewModel.Entries, Has.Count.EqualTo(1));

        viewModel.SelectedEntry = viewModel.Entries[0];
        await viewModel.RestoreCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(source), Is.True);
            Assert.That(viewModel.Entries, Is.Empty);
        });
    }
}
