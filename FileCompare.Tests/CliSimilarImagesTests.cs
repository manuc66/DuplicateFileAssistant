using System.Text.Json;
using DuplicateFileAssistant.Cli;
using ImageCompare;
using SkiaSharp;

namespace FileCompare.Tests;

[TestFixture]
public class CliSimilarImagesTests
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
    public async Task SimilarImagesCommandOutputsJson()
    {
        using SKBitmap original = TestImages.CreatePattern();
        using SKBitmap resized = TestImages.Resize(original, 320, 240);
        using SKBitmap other = TestImages.CreateOtherPattern();

        string originalPath = _temp.CreateFile("original.png", TestImages.Encode(original, png: true));
        string copyPath = _temp.CreateFile("copy.jpg", TestImages.Encode(resized, png: false, quality: 35));
        _temp.CreateFile("other.png", TestImages.Encode(other, png: true));

        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[]
            {
                "similar-images", "--path", _temp.Root, "--json", "--no-cache", "--min-size", "0"
            },
            output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        JsonElement results = root.GetProperty("results");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(root.GetProperty("applied").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("affected").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("threshold").GetInt32(), Is.EqualTo(SimilarImageFinder.DefaultMaxDistance));
            Assert.That(root.GetProperty("hashed").GetInt32(), Is.EqualTo(3));
            Assert.That(results.GetArrayLength(), Is.EqualTo(1));
            Assert.That(results[0].GetProperty("files").GetArrayLength(), Is.EqualTo(2));
            Assert.That(
                results[0].GetProperty("files").EnumerateArray().Select(element => element.GetString()),
                Is.EquivalentTo(new[] { originalPath, copyPath }));
        });
    }

    [Test]
    public async Task SimilarImagesIsReportOnly()
    {
        using SKBitmap original = TestImages.CreatePattern();
        string originalPath = _temp.CreateFile("original.png", TestImages.Encode(original, png: true));
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "similar-images", "--path", _temp.Root, "--apply", "--no-cache", "--min-size", "0" },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(File.Exists(originalPath), Is.True);
            Assert.That(output.ToString(), Does.Contain("report-only"));
        });
    }

    [Test]
    public async Task SimilarImagesUsesTheConfiguredCache()
    {
        using SKBitmap original = TestImages.CreatePattern();
        using SKBitmap resized = TestImages.Resize(original, 320, 240);
        _temp.CreateFile("original.png", TestImages.Encode(original, png: true));
        _temp.CreateFile("copy.jpg", TestImages.Encode(resized, png: false, quality: 40));

        string cachePath = Path.Combine(_temp.Root, "cache", "hashes.json");
        StringWriter output = new();

        await CliRunner.RunAsync(
            new[]
            {
                "similar-images", "--path", _temp.Root, "--json", "--cache-path", cachePath, "--min-size", "0"
            },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(cachePath), Is.True);
            Assert.That(new FileInfo(cachePath).Length, Is.GreaterThan(2));
        });
    }
}
