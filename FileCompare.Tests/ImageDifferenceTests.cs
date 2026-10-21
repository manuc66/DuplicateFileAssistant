using DuplicateAssistant.Business;
using SkiaSharp;

namespace FileCompare.Tests;

[TestFixture]
public class ImageDifferenceTests
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
    public void IdenticalImagesHaveAlmostNoDifference()
    {
        using SKBitmap image = TestImages.CreatePattern(400, 300);
        string first = _temp.CreateFile("first.png", TestImages.Encode(image, png: true));
        string second = _temp.CreateFile("second.png", TestImages.Encode(image, png: true));

        ImageDifference.Result? result = ImageDifference.Create(first, second);

        Assert.That(result, Is.Not.Null);

        using SKBitmap difference = SKBitmap.Decode(result!.Png);

        Assert.Multiple(() =>
        {
            Assert.That(result.MeanDifference, Is.LessThan(0.5));
            Assert.That(difference.Width, Is.EqualTo(ImageDifference.Width));
            Assert.That(difference.Height, Is.EqualTo(ImageDifference.Height));
        });
    }

    [Test]
    public void EditedImageShowsADifference()
    {
        using SKBitmap image = TestImages.CreatePattern(400, 300);
        using SKBitmap edited = image.Copy();

        using (SKCanvas canvas = new(edited))
        using (SKPaint paint = new() { Color = SKColors.Black })
        {
            canvas.DrawRect(60, 60, 120, 90, paint);
        }

        string first = _temp.CreateFile("first.png", TestImages.Encode(image, png: true));
        string second = _temp.CreateFile("second.png", TestImages.Encode(edited, png: true));

        ImageDifference.Result? result = ImageDifference.Create(first, second);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.MeanDifference, Is.GreaterThan(1.0));
    }

    [Test]
    public void ResizedAndRecompressedCopyStaysSmall()
    {
        using SKBitmap image = TestImages.CreatePattern(800, 600);
        using SKBitmap resized = TestImages.Resize(image, 400, 300);
        string first = _temp.CreateFile("first.png", TestImages.Encode(image, png: true));
        string second = _temp.CreateFile("second.jpg", TestImages.Encode(resized, png: false, quality: 50));

        ImageDifference.Result? result = ImageDifference.Create(first, second);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.MeanDifference, Is.LessThan(8.0));
    }
}
