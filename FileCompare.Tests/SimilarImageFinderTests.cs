using FileCompare;
using ImageCompare;
using SkiaSharp;

namespace FileCompare.Tests;

[TestFixture]
public class SimilarImageFinderTests
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
    public void GroupsResizedAndRecompressedCopiesOnly()
    {
        using SKBitmap original = TestImages.CreatePattern();
        using SKBitmap resized = TestImages.Resize(original, 400, 300);
        using SKBitmap other = TestImages.CreateOtherPattern();

        string originalPath = _temp.CreateFile("original.png", TestImages.Encode(original, png: true));
        string copyPath = _temp.CreateFile(Path.Combine("sub", "copy.jpg"),
            TestImages.Encode(resized, png: false, quality: 40));
        string otherPath = _temp.CreateFile("other.png", TestImages.Encode(other, png: true));
        _temp.CreateFile("notes.txt", "not an image");

        StringWriter progress = new();
        SimilarImageScanResult result = SimilarImageFinder.Find(
            _temp.Root,
            SearchOption.AllDirectories,
            FileSearchFilter.FromText(0, "png,jpg", null),
            new SimilarImageOptions { MaxDistance = SimilarImageFinder.DefaultMaxDistance },
            progress,
            updateProgress: null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failures, Is.Empty);
            Assert.That(result.HashedCount, Is.EqualTo(3));
            Assert.That(result.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Groups[0].Images.Select(member => member.Image.Path),
                Is.EquivalentTo(new[] { originalPath, copyPath }));
            Assert.That(result.Groups[0].Images.Select(member => member.Image.Path), Does.Not.Contain(otherPath));
            Assert.That(result.RedundantCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void DifferentAspectRatiosAreNotGrouped()
    {
        using SKBitmap original = TestImages.CreatePattern(800, 400);
        using SKBitmap stretched = TestImages.Resize(original, 400, 400);

        _temp.CreateFile("wide.png", TestImages.Encode(original, png: true));
        _temp.CreateFile("square.jpg", TestImages.Encode(stretched, png: false, quality: 90));

        SimilarImageScanResult result = SimilarImageFinder.Find(
            _temp.Root,
            SearchOption.AllDirectories,
            FileSearchFilter.FromText(0, "png,jpg", null),
            new SimilarImageOptions { MaxDistance = 8 },
            TextWriter.Null,
            updateProgress: null,
            CancellationToken.None);

        Assert.That(result.Groups, Is.Empty);
    }

    [Test]
    public void UndecodableImagesAreReportedAndSkipped()
    {
        _temp.CreateFile("broken.png", "definitely not a PNG");

        SimilarImageScanResult result = SimilarImageFinder.Find(
            _temp.Root,
            SearchOption.AllDirectories,
            FileSearchFilter.FromText(0, "png", null),
            new SimilarImageOptions { MaxDistance = 8 },
            TextWriter.Null,
            updateProgress: null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HashedCount, Is.Zero);
            Assert.That(result.Failures, Has.Count.EqualTo(1));
            Assert.That(result.Groups, Is.Empty);
        });
    }

    [Test]
    public void MinimumSizeIsRespected()
    {
        using SKBitmap small = TestImages.CreatePattern(40, 30);
        _temp.CreateFile("small.png", TestImages.Encode(small, png: true));

        SimilarImageScanResult result = SimilarImageFinder.Find(
            _temp.Root,
            SearchOption.AllDirectories,
            FileSearchFilter.FromText(1024 * 1024, "png", null),
            new SimilarImageOptions { MaxDistance = 8 },
            TextWriter.Null,
            updateProgress: null,
            CancellationToken.None);

        Assert.That(result.HashedCount, Is.Zero);
    }

    [Test]
    public void HashesAreCachedBetweenRuns()
    {
        using SKBitmap original = TestImages.CreatePattern();
        using SKBitmap resized = TestImages.Resize(original, 400, 300);

        _temp.CreateFile("original.png", TestImages.Encode(original, png: true));
        _temp.CreateFile("copy.jpg", TestImages.Encode(resized, png: false, quality: 50));

        string cachePath = Path.Combine(_temp.Root, "cache", "hashes.json");
        FileSearchFilter filter = FileSearchFilter.FromText(0, "png,jpg", null);

        ScanCache cache = new(cachePath);

        SimilarImageScanResult first = SimilarImageFinder.Find(
            _temp.Root,
            SearchOption.AllDirectories,
            filter,
            new SimilarImageOptions { MaxDistance = 8 },
            TextWriter.Null,
            updateProgress: null,
            CancellationToken.None,
            cache: cache);

        cache.Save();

        SimilarImageScanResult second = SimilarImageFinder.Find(
            _temp.Root,
            SearchOption.AllDirectories,
            filter,
            new SimilarImageOptions { MaxDistance = 8 },
            TextWriter.Null,
            updateProgress: null,
            CancellationToken.None,
            cache: new ScanCache(cachePath));

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(cachePath), Is.True);
            Assert.That(second.Groups, Has.Count.EqualTo(first.Groups.Count));
            Assert.That(second.Groups[0].Images, Has.Count.EqualTo(first.Groups[0].Images.Count));
        });
    }
}
