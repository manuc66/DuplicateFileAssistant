using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.Business;
using DuplicateAssistant.ViewModels;
using FileCompare;
using ImageCompare;
using SkiaSharp;

namespace FileCompare.Tests;

[TestFixture]
public class SemanticImageTests
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
    public void SemanticPassGroupsPicturesWithDifferentFraming()
    {
        using SKBitmap first = TestImages.CreatePattern();
        using SKBitmap second = TestImages.CreateOtherPattern();

        string firstPath = _temp.CreateFile("first.png", TestImages.Encode(first, png: true));
        string secondPath = _temp.CreateFile("second.png", TestImages.Encode(second, png: true));

        FakeEmbeddingProvider embeddings = new(new Dictionary<string, float[]>
        {
            [firstPath] = new[] { 1f, 0f, 0f },
            [secondPath] = new[] { 1f, 0f, 0f }
        });

        SimilarImageScanResult result = Find(new SimilarImageOptions
        {
            Embeddings = embeddings,
            SemanticThreshold = 0.9
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Groups[0].Kind, Is.EqualTo(SimilarImageKind.Semantic));
            Assert.That(result.Groups[0].Images, Has.Count.EqualTo(2));
            Assert.That(result.Groups[0].MinimumSimilarity, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(result.EmbeddedCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void SemanticThresholdRejectsDissimilarEmbeddings()
    {
        using SKBitmap first = TestImages.CreatePattern();
        using SKBitmap second = TestImages.CreateOtherPattern();

        string firstPath = _temp.CreateFile("first.png", TestImages.Encode(first, png: true));
        string secondPath = _temp.CreateFile("second.png", TestImages.Encode(second, png: true));

        FakeEmbeddingProvider embeddings = new(new Dictionary<string, float[]>
        {
            [firstPath] = new[] { 1f, 0f, 0f },
            [secondPath] = new[] { 0.6f, 0.8f, 0f }
        });

        SimilarImageScanResult result = Find(new SimilarImageOptions
        {
            Embeddings = embeddings,
            SemanticThreshold = 0.9
        });

        Assert.That(result.Groups, Is.Empty);
    }

    [Test]
    public void SemanticMarginRejectsAmbiguousMatches()
    {
        using SKBitmap first = TestImages.CreatePattern();
        using SKBitmap second = TestImages.CreateOtherPattern();
        using SKBitmap third = TestImages.CreateStripesPattern();

        string firstPath = _temp.CreateFile("a.png", TestImages.Encode(first, png: true));
        string secondPath = _temp.CreateFile("b.png", TestImages.Encode(second, png: true));
        string thirdPath = _temp.CreateFile("c.png", TestImages.Encode(third, png: true));

        FakeEmbeddingProvider embeddings = new(new Dictionary<string, float[]>
        {
            [firstPath] = new[] { 1f, 0f, 0f },
            [secondPath] = new[] { 0.9f, 0.4359f, 0f },
            [thirdPath] = new[] { 0.9f, 0.2065f, 0.3839f }
        });

        SimilarImageScanResult result = Find(new SimilarImageOptions
        {
            Embeddings = embeddings,
            SemanticThreshold = 0.8,
            SemanticMargin = 0.05
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Groups[0].Images, Has.Count.EqualTo(2));
            Assert.That(result.Groups[0].Images.Select(member => member.Image.Path),
                Does.Not.Contain(thirdPath));
        });
    }

    [Test]
    public void SimHashPathAlsoGroupsIdenticalEmbeddings()
    {
        using SKBitmap first = TestImages.CreatePattern();
        using SKBitmap second = TestImages.CreateOtherPattern();

        string firstPath = _temp.CreateFile("first.png", TestImages.Encode(first, png: true));
        string secondPath = _temp.CreateFile("second.png", TestImages.Encode(second, png: true));

        FakeEmbeddingProvider embeddings = new(new Dictionary<string, float[]>
        {
            [firstPath] = new[] { 1f, 0f, 0f },
            [secondPath] = new[] { 1f, 0f, 0f }
        });

        SimilarImageScanResult result = Find(new SimilarImageOptions
        {
            Embeddings = embeddings,
            SemanticThreshold = 0.9,
            BruteForceEmbeddingLimit = 0
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Groups[0].Images, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void QualityScorerChoosesTheKeeper()
    {
        using SKBitmap original = TestImages.CreatePattern();
        using SKBitmap resized = TestImages.Resize(original, 792, 594);

        string originalPath = _temp.CreateFile("original.png", TestImages.Encode(original, png: true));
        string copyPath = _temp.CreateFile("copy.jpg", TestImages.Encode(resized, png: false, quality: 40));

        FakeQualityScorer quality = new(path =>
            PathUtilities.PathComparer.Equals(path, copyPath) ? 0.9 : 0.2);

        SimilarImageScanResult result = Find(new SimilarImageOptions { Quality = quality });

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Groups[0].Keeper.Path, Is.EqualTo(copyPath));
            Assert.That(result.Groups[0].KeeperQuality, Is.EqualTo(0.9).Within(0.0001));
            Assert.That(result.Groups[0].Images[0].Image.Path, Is.EqualTo(copyPath));
            Assert.That(result.Groups[0].Images[0].Quality, Is.EqualTo(0.9).Within(0.0001));
            Assert.That(result.Groups[0].Images[1].Quality, Is.EqualTo(0.2).Within(0.0001));
        });
    }

    [Test]
    public void FailedEmbeddingsKeepPerceptualGroups()
    {
        using SKBitmap original = TestImages.CreatePattern();
        using SKBitmap resized = TestImages.Resize(original, 400, 300);

        string originalPath = _temp.CreateFile("original.png", TestImages.Encode(original, png: true));
        string copyPath = _temp.CreateFile("copy.jpg", TestImages.Encode(resized, png: false, quality: 50));

        FakeEmbeddingProvider embeddings = new(new Dictionary<string, float[]>
        {
            [originalPath] = new[] { 1f, 0f, 0f }
        });

        SimilarImageScanResult result = Find(new SimilarImageOptions { Embeddings = embeddings });

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Groups[0].Kind, Is.EqualTo(SimilarImageKind.Perceptual));
            Assert.That(result.EmbeddedCount, Is.EqualTo(1));
            Assert.That(result.Failures, Has.Count.EqualTo(1));
            Assert.That(result.Failures[0].Stage, Is.EqualTo(SimilarImageFailureStage.Embedding));
        });
    }

    [Test]
    public void HeuristicScorerRanksSharpAndLargeImagesHigher()
    {
        using SKBitmap original = TestImages.CreatePattern(800, 600);
        using SKBitmap blurred = new(original.Info);

        using (SKCanvas canvas = new(blurred))
        using (SKPaint paint = new() { ImageFilter = SKImageFilter.CreateBlur(6, 6) })
        {
            canvas.DrawBitmap(original, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), paint);
        }

        using SKBitmap small = TestImages.Resize(original, 200, 150);

        string originalPath = _temp.CreateFile("sharp.png", TestImages.Encode(original, png: true));
        string blurredPath = _temp.CreateFile("blurred.png", TestImages.Encode(blurred, png: true));
        string smallPath = _temp.CreateFile("small.png", TestImages.Encode(small, png: true));

        using HeuristicQualityScorer scorer = new();

        double sharpScore = scorer.Score(originalPath);
        double blurredScore = scorer.Score(blurredPath);
        double smallScore = scorer.Score(smallPath);

        Assert.Multiple(() =>
        {
            Assert.That(sharpScore, Is.GreaterThan(blurredScore));
            Assert.That(sharpScore, Is.GreaterThan(smallScore));
            Assert.That(sharpScore, Is.InRange(0.0, 1.0));
        });
    }

    [Test]
    public void ThumbnailIsSmallAndDecodable()
    {
        using SKBitmap original = TestImages.CreatePattern(800, 600);
        string path = _temp.CreateFile("thumbnail-source.png", TestImages.Encode(original, png: true));

        byte[]? png = ThumbnailLoader.LoadPng(path, 192, 144);

        Assert.That(png, Is.Not.Null);

        using SKBitmap thumbnail = SKBitmap.Decode(png);

        Assert.Multiple(() =>
        {
            Assert.That(thumbnail.Width, Is.LessThanOrEqualTo(192));
            Assert.That(thumbnail.Height, Is.LessThanOrEqualTo(144));
            Assert.That(thumbnail.Width, Is.GreaterThan(0));
            Assert.That(thumbnail.Height, Is.GreaterThan(0));
        });
    }

    [Test]
    public void SimilarImageOptionsAreRestored()
    {
        using TempDirectory temp = new();
        string filtersFile = Path.Combine(temp.Root, "filters.json");
        FilterSettingsStore store = new(filtersFile);
        store.Save("similar-images", FilterSettings.Default with
        {
            Extensions = ".png",
            MinimumSizeText = "20 KB",
            UseSemantic = true,
            UseKeeper = false,
            UseKeeperModel = true,
            SemanticThreshold = 0.9,
            AllowModelDownload = false
        });

        SimilarImageViewModel viewModel = new(
            new Trash(Path.Combine(temp.Root, "trash")),
            temp.Root,
            new FileManagerHandler(),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker(),
            filterSettingsStore: store);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ExtensionSelection.ToFilterString(), Is.EqualTo(".png"));
            Assert.That(viewModel.MinimumFileSizeText, Is.EqualTo("20 KB"));
            Assert.That(viewModel.UseSemantic, Is.True);
            Assert.That(viewModel.UseKeeper, Is.False);
            Assert.That(viewModel.UseKeeperModel, Is.True);
            Assert.That(viewModel.SemanticThreshold, Is.EqualTo(0.9m));
            Assert.That(viewModel.AllowModelDownload, Is.False);
        });
    }

    [AvaloniaTest]
    public async Task GuiFindsVariantsAndSelectsTheLargestAsKeeper()
    {
        using SKBitmap original = TestImages.CreatePattern();
        using SKBitmap resized = TestImages.Resize(original, 600, 450);

        // The small copy sorts first, so it becomes the group leader. The
        // suggested keeper must still be the larger original.
        string copyPath = _temp.CreateFile("aa-copy.jpg", TestImages.Encode(resized, png: false, quality: 40));
        string originalPath = _temp.CreateFile("zz-original.png", TestImages.Encode(original, png: true));

        SimilarImageViewModel viewModel = new(
            new Trash(Path.Combine(_temp.Root, "trash")),
            _temp.Root,
            new FileManagerHandler(),
            new FakeDeleteConfirmation(),
            new FakeFolderPicker())
        {
            UseSemantic = false,
            UseKeeper = true,
            LoadThumbnails = false,
            SubFolder = true,
            MinimumFileSizeText = "0",
            FileExtensions = "png,jpg"
        };

        await viewModel.SearchCommand.Execute(Unit.Default).LastAsync();
        await TestUi.WaitForAsync(
            () => viewModel.DuplicateCaseItems.Count == 1,
            "the similar-image group to appear");

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateCaseItems, Has.Count.EqualTo(1));
            Assert.That(viewModel.DuplicateCase, Is.Not.Null);
            Assert.That(viewModel.DuplicateCase!.Keeper!.FilePath, Is.EqualTo(originalPath));
            Assert.That(viewModel.DuplicateCase.Files.Select(file => file.FilePath),
                Is.EquivalentTo(new[] { copyPath, originalPath }));
            Assert.That(viewModel.DuplicateCase.Kind, Is.EqualTo(SimilarImageKind.Perceptual));
            Assert.That(viewModel.DuplicateCase.Files.All(file => !file.CanHardlink), Is.True);
            Assert.That(viewModel.DuplicateCase.Files.All(file => !string.IsNullOrWhiteSpace(file.MatchDetail)),
                Is.True);
            Assert.That(
                viewModel.DuplicateCase.Files.First(file => file.FilePath == originalPath).MatchDetail,
                Does.Contain("dHash"));
            Assert.That(viewModel.Summary, Does.Contain("group"));
        });
    }

    [Test]
    public void QualityCacheRoundTripsAndInvalidates()
    {
        string imagePath = _temp.CreateFile("image.png", "first");
        string cachePath = Path.Combine(_temp.Root, "cache", "quality.bin");

        FakeQualityScorer inner = new(_ => 0.42);
        QualityCache cache = new(cachePath, inner.Id);
        CachedQualityScorer scorer = new(inner, cache);

        double first = scorer.Score(imagePath);
        cache.Save();

        int calls = 0;
        FakeQualityScorer reloadedInner = new(_ =>
        {
            calls++;
            return 0.42;
        });

        CachedQualityScorer reloaded = new(
            reloadedInner,
            new QualityCache(cachePath, reloadedInner.Id));

        double cached = reloaded.Score(imagePath);
        int callsAfterHit = calls;

        File.AppendAllText(imagePath, "more");
        double recomputed = reloaded.Score(imagePath);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(0.42).Within(0.0001));
            Assert.That(cached, Is.EqualTo(0.42).Within(0.0001));
            Assert.That(callsAfterHit, Is.Zero);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(recomputed, Is.EqualTo(0.42).Within(0.0001));
        });
    }

    [Test]
    public void EmbeddingCachePrunesDeletedFiles()
    {
        string firstPath = _temp.CreateFile("first.png", "first");
        string secondPath = _temp.CreateFile("second.png", "second");
        string cachePath = Path.Combine(_temp.Root, "cache", "embeddings.bin");

        EmbeddingCache cache = new(cachePath, 3);
        cache.GetOrCompute(firstPath, _ => new[] { 1f, 2f, 3f });
        cache.GetOrCompute(secondPath, _ => new[] { 4f, 5f, 6f });

        File.Delete(firstPath);
        int removed = cache.PruneMissingFiles();

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(cache.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void EmbeddingCacheRoundTripsAndInvalidates()
    {
        string imagePath = _temp.CreateFile("image.png", "first");
        string cachePath = Path.Combine(_temp.Root, "cache", "embeddings.bin");

        EmbeddingCache cache = new(cachePath, 3);
        float[] first = cache.GetOrCompute(imagePath, _ => new[] { 1f, 2f, 3f });
        cache.Save();

        EmbeddingCache reloaded = new(cachePath, 3);
        float[] cached = reloaded.GetOrCompute(imagePath, _ =>
            throw new InvalidOperationException("The cache should have answered."));

        File.AppendAllText(imagePath, "more");

        float[] recomputed = reloaded.GetOrCompute(imagePath, _ => new[] { 4f, 5f, 6f });

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new[] { 1f, 2f, 3f }));
            Assert.That(cached, Is.EqualTo(new[] { 1f, 2f, 3f }));
            Assert.That(recomputed, Is.EqualTo(new[] { 4f, 5f, 6f }));
        });
    }

    private SimilarImageScanResult Find(SimilarImageOptions options)
    {
        return SimilarImageFinder.Find(
            _temp.Root,
            SearchOption.AllDirectories,
            FileSearchFilter.FromText(0, "png,jpg", null),
            options,
            TextWriter.Null,
            updateProgress: null,
            CancellationToken.None);
    }

    private sealed class FakeEmbeddingProvider : IImageEmbeddingProvider
    {
        private readonly IReadOnlyDictionary<string, float[]> _vectors;

        public FakeEmbeddingProvider(IReadOnlyDictionary<string, float[]> vectors)
        {
            _vectors = vectors;
        }

        public string Id => "fake";

        public int Dimensions => 3;

        public float[] Embed(string path)
        {
            return _vectors[path];
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeQualityScorer : IQualityScorer
    {
        private readonly Func<string, double> _score;

        public FakeQualityScorer(Func<string, double> score)
        {
            _score = score;
        }

        public string Id => "fake-quality";

        public double Score(string path)
        {
            return _score(path);
        }

        public void Dispose()
        {
        }
    }
}
