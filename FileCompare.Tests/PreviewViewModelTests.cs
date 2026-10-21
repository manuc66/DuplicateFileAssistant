using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.ViewModels;
using DuplicateAssistant.Views;
using SkiaSharp;

namespace FileCompare.Tests;

[TestFixture]
public class PreviewViewModelTests
{
    private TempDirectory _temp = null!;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempDirectory();
    }

    [TearDown]
    public void TearDown()
    {
        _temp.Dispose();
    }

    [Test]
    public void ZoomIsClampedAndReset()
    {
        PreviewViewModel viewModel = new("unused.png");

        for (int index = 0; index < 100; index++)
        {
            viewModel.ZoomIn();
        }

        Assert.That(viewModel.Zoom, Is.EqualTo(PreviewViewModel.MaximumZoom));

        for (int index = 0; index < 100; index++)
        {
            viewModel.ZoomOut();
        }

        Assert.That(viewModel.Zoom, Is.EqualTo(PreviewViewModel.MinimumZoom));

        viewModel.ResetZoom();

        Assert.That(viewModel.Zoom, Is.EqualTo(1.0).Within(0.0001));
    }

    [AvaloniaTest]
    public async Task LoadsTheImage()
    {
        using SKBitmap image = TestImages.CreatePattern(800, 600);
        string path = _temp.CreateFile("preview.png", TestImages.Encode(image, png: true));
        PreviewViewModel viewModel = new(path);

        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Image, Is.Not.Null);
            Assert.That(viewModel.Title, Is.EqualTo("preview.png"));
        });
    }

    [AvaloniaTest]
    public async Task LoadsComparisonAndDifference()
    {
        using SKBitmap image = TestImages.CreatePattern(800, 600);
        using SKBitmap edited = image.Copy();

        using (SKCanvas canvas = new(edited))
        using (SKPaint paint = new() { Color = SKColors.Black })
        {
            canvas.DrawRect(100, 80, 180, 120, paint);
        }

        string first = _temp.CreateFile("first.png", TestImages.Encode(image, png: true));
        string second = _temp.CreateFile("second.png", TestImages.Encode(edited, png: true));
        PreviewViewModel viewModel = new(first, second);

        await viewModel.LoadAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasComparison, Is.True);
            Assert.That(viewModel.ComparisonImage, Is.Not.Null);
            Assert.That(viewModel.DifferenceImage, Is.Not.Null);
            Assert.That(viewModel.DifferenceText, Is.Not.Empty);
        });
    }

    [Test]
    public void SingleImageHasNoComparison()
    {
        PreviewViewModel viewModel = new("unused.png");

        Assert.That(viewModel.HasComparison, Is.False);
    }

    [AvaloniaTest]
    public void WindowLoads()
    {
        PreviewWindow window = new(new PreviewViewModel("unused.png"));

        Assert.That(window.Content, Is.Not.Null);
    }

    [AvaloniaTest]
    public void TogglesFullScreen()
    {
        PreviewWindow window = new(new PreviewViewModel("unused.png"));
        window.Show();

        window.ToggleFullScreen();
        Assert.That(window.WindowState, Is.EqualTo(WindowState.FullScreen));

        window.ToggleFullScreen();
        Assert.That(window.WindowState, Is.EqualTo(WindowState.Normal));

        window.Close();
    }
}
