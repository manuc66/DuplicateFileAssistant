using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using DuplicateAssistant.Controls;
using DuplicateAssistant.ViewModels;
using DuplicateAssistant.Views;

[assembly: AvaloniaTestApplication(typeof(FileCompare.Tests.TestAppBuilder))]

namespace FileCompare.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<DuplicateAssistant.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}

[TestFixture]
public class HeadlessUiTests
{
    [AvaloniaTest]
    public void MainWindow_LoadsItsContent()
    {
        MainWindow window = new();

        Assert.That(window.Content, Is.Not.Null);
    }

    [AvaloniaTest]
    public void MainWindow_SidebarSelectsTheMatchingPage()
    {
        MainWindow window = new();

        ListBox settingsNavigation = window.FindControl<ListBox>("UtilityNavigation")!;
        TabControl pages = window.FindControl<TabControl>("Pages")!;

        settingsNavigation.SelectedIndex = 1;

        Assert.That(pages.SelectedIndex, Is.EqualTo(13));
    }

    [AvaloniaTest]
    public void ConfirmDeleteWindow_LoadsItsMessage()
    {
        ConfirmDeleteWindow window = new("photo.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(window.Title, Is.EqualTo("Confirm deletion"));
            Assert.That(window.Content, Is.Not.Null);
        });
    }

    [AvaloniaTest]
    public void SimilarImageUserControl_Loads()
    {
        SimilarImageUserControl control = new();

        Assert.That(control.Content, Is.Not.Null);
    }

    [AvaloniaTest]
    public void ExtensionPicker_Loads()
    {
        ExtensionPicker control = new() { DataContext = new ExtensionSelectionViewModel() };

        Assert.That(control.Content, Is.Not.Null);
    }

    [AvaloniaTest]
    public void SettingsUserControl_LoadsWithItsViewModel()
    {
        using TempDirectory temp = new();
        TrashSettingsStore store = new(
            Path.Combine(temp.Root, "settings.json"),
            new TrashSettings(TrashMode.Application, Path.Combine(temp.Root, "trash")));

        SettingsUserControl control = new()
        {
            DataContext = new SettingsViewModel(store, new FakeFolderPicker())
        };

        Assert.That(control.Content, Is.Not.Null);
    }
}
