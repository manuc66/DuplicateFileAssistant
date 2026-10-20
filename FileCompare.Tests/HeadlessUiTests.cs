using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
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
    public void ConfirmDeleteWindow_LoadsItsMessage()
    {
        ConfirmDeleteWindow window = new("photo.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(window.Title, Is.EqualTo("Confirm deletion"));
            Assert.That(window.Content, Is.Not.Null);
        });
    }
}
