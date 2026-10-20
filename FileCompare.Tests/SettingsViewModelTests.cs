using System.Reactive;
using System.Reactive.Linq;
using DuplicateAssistant.ViewModels;
using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class SettingsViewModelTests
{
    [Test]
    public async Task SavePersistsTheSelectedModeAndFolder()
    {
        using TempDirectory temp = new();
        string settingsFile = Path.Combine(temp.Root, "settings.json");
        string folder = Path.Combine(temp.Root, "custom-trash");
        TrashSettingsStore store = new(settingsFile, new TrashSettings(TrashMode.Application, folder));
        SettingsViewModel viewModel = new(store, new FakeFolderPicker());

        viewModel.UseSystemTrash = true;
        await viewModel.SaveCommand.Execute(Unit.Default).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(store.Current.Mode, Is.EqualTo(TrashMode.System));
            Assert.That(store.Current.TrashFolder, Is.EqualTo(folder));
            Assert.That(viewModel.Status, Does.Contain("Saved"));
        });
    }

    [Test]
    public async Task SaveRejectsTheFilesystemRoot()
    {
        using TempDirectory temp = new();
        string folder = Path.Combine(temp.Root, "custom-trash");
        TrashSettingsStore store = new(Path.Combine(temp.Root, "settings.json"),
            new TrashSettings(TrashMode.Application, folder));
        SettingsViewModel viewModel = new(store, new FakeFolderPicker())
        {
            TrashFolder = Path.GetPathRoot(Path.GetFullPath(temp.Root)) ?? "/"
        };

        await viewModel.SaveCommand.Execute(Unit.Default).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(store.Current.TrashFolder, Is.EqualTo(folder));
            Assert.That(viewModel.Status, Does.Contain("filesystem root"));
        });
    }
}
