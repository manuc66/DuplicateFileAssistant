using ImageCompare;
using SkiaSharp;

namespace FileCompare.Tests;

[TestFixture]
public class PerceptualHasherTests
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
    public void ResizedAndRecompressedCopyStaysClose()
    {
        using SKBitmap original = TestImages.CreatePattern();
        string originalPath = _temp.CreateFile("original.png", TestImages.Encode(original, png: true));

        using SKBitmap resized = TestImages.Resize(original, 320, 240);
        string copyPath = _temp.CreateFile("copy.jpg", TestImages.Encode(resized, png: false, quality: 35));

        PerceptualHash originalHash = PerceptualHasher.Compute(originalPath);
        PerceptualHash copyHash = PerceptualHasher.Compute(copyPath);

        Assert.Multiple(() =>
        {
            Assert.That(copyHash.DistanceTo(originalHash), Is.LessThanOrEqualTo(4));
            Assert.That(copyHash.HasSimilarAspectRatio(originalHash), Is.True);
        });
    }

    [Test]
    public void DifferentPicturesAreFarApart()
    {
        using SKBitmap first = TestImages.CreatePattern();
        using SKBitmap second = TestImages.CreateOtherPattern();

        string firstPath = _temp.CreateFile("first.png", TestImages.Encode(first, png: true));
        string secondPath = _temp.CreateFile("second.png", TestImages.Encode(second, png: true));

        int distance = PerceptualHasher.Compute(firstPath).DistanceTo(PerceptualHasher.Compute(secondPath));

        Assert.That(distance, Is.GreaterThan(16));
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void ExifOrientationIsApplied(int orientation)
    {
        using SKBitmap source = TestImages.CreatePattern(640, 480);
        byte[] jpeg = TestImages.Encode(source, png: false, quality: 95);

        string taggedPath = _temp.CreateFile(
            $"tagged-{orientation}.jpg",
            TestImages.WithExifOrientation(jpeg, (ushort)orientation));

        using SKBitmap upright = TestImages.Orient(source, orientation);
        string uprightPath = _temp.CreateFile(
            $"upright-{orientation}.jpg",
            TestImages.Encode(upright, png: false, quality: 95));

        PerceptualHash tagged = PerceptualHasher.Compute(taggedPath);
        PerceptualHash expected = PerceptualHasher.Compute(uprightPath);

        Assert.Multiple(() =>
        {
            Assert.That(tagged.DistanceTo(expected), Is.LessThanOrEqualTo(4));
            Assert.That(tagged.Width, Is.EqualTo(upright.Width));
            Assert.That(tagged.Height, Is.EqualTo(upright.Height));
        });
    }

    [Test]
    public void TransparentPixelsAreCompositedOnWhite()
    {
        using SKBitmap transparent = new(new SKImageInfo(800, 600, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (SKCanvas canvas = new(transparent))
        {
            canvas.Clear(SKColors.Transparent);
            using SKPaint paint = new() { Color = SKColors.Red, IsAntialias = true };
            canvas.DrawCircle(400, 300, 200, paint);
        }

        using SKBitmap onWhite = new(new SKImageInfo(800, 600));
        using (SKCanvas canvas = new(onWhite))
        {
            canvas.Clear(SKColors.White);
            using SKPaint paint = new() { Color = SKColors.Red, IsAntialias = true };
            canvas.DrawCircle(400, 300, 200, paint);
        }

        string transparentPath = _temp.CreateFile("transparent.png", TestImages.Encode(transparent, png: true));
        string whitePath = _temp.CreateFile("white.png", TestImages.Encode(onWhite, png: true));

        int distance = PerceptualHasher.Compute(transparentPath)
            .DistanceTo(PerceptualHasher.Compute(whitePath));

        Assert.That(distance, Is.Zero);
    }

    [Test]
    public void TinyImageIsSupported()
    {
        using SKBitmap tiny = TestImages.CreatePattern(3, 2);
        string path = _temp.CreateFile("tiny.png", TestImages.Encode(tiny, png: true));

        PerceptualHash hash = PerceptualHasher.Compute(path);

        Assert.Multiple(() =>
        {
            Assert.That(hash.Width, Is.EqualTo(3));
            Assert.That(hash.Height, Is.EqualTo(2));
        });
    }

    [Test]
    public void CacheBytesRoundTrip()
    {
        PerceptualHash expected = new(0xDEADBEEFCAFEBABE, 1920, 1080);

        byte[] bytes = PerceptualHasher.ToCacheBytes(expected);

        Assert.Multiple(() =>
        {
            Assert.That(PerceptualHasher.TryFromCacheBytes(bytes, out PerceptualHash actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(PerceptualHasher.TryFromCacheBytes(new byte[3], out _), Is.False);
        });
    }

    [Test]
    public void CorruptImageIsRejected()
    {
        string path = _temp.CreateFile("broken.png", "not an image");

        Assert.Throws<InvalidDataException>(() => PerceptualHasher.Compute(path));
    }
}
