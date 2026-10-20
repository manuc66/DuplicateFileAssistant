using FileCompare;
using ImageCompare;

namespace FileCompare.Tests;

[TestFixture]
public class CacheInspectorTests
{
    private TempDirectory _temp = null!;
    private string _cacheDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempDirectory();
        _cacheDirectory = Path.Combine(_temp.Root, "cache");
        Directory.CreateDirectory(_cacheDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        _temp.Dispose();
    }

    [Test]
    public void InspectOfAMissingDirectoryIsEmpty()
    {
        CacheSnapshot snapshot = CacheInspector.Inspect(
            Path.Combine(_temp.Root, "missing"),
            Path.Combine(_temp.Root, "models"));

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Entries, Is.Empty);
            Assert.That(snapshot.TotalBytes, Is.Zero);
        });
    }

    [Test]
    public void InspectReportsEveryCacheKind()
    {
        string file = _temp.CreateFile("image.jpg", "content");
        ScanCache scan = new(Path.Combine(_cacheDirectory, "scan-cache.json"));
        scan.GetOrCompute("sha256", file, File.ReadAllBytes);
        scan.Save();

        EmbeddingCache embeddings = new(Path.Combine(_cacheDirectory, "embeddings-model.bin"), dimensions: 2);
        embeddings.GetOrCompute(file, _ => new[] { 0.1f, 0.2f });
        embeddings.Save();

        QualityCache quality = new(Path.Combine(_cacheDirectory, "quality-heuristic.bin"), "heuristic");
        quality.GetOrCompute(file, _ => 0.75);
        quality.Save();

        string modelsDirectory = Path.Combine(_temp.Root, "models");
        string modelFolder = Directory.CreateDirectory(Path.Combine(modelsDirectory, "model")).FullName;
        File.WriteAllText(Path.Combine(modelFolder, "weights.onnx"), "weights");
        File.WriteAllText(Path.Combine(_cacheDirectory, "garbage.bin"), "not a cache");

        CacheSnapshot snapshot = CacheInspector.Inspect(_cacheDirectory, modelsDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Entries.Select(entry => entry.Kind),
                Is.EquivalentTo(new[] { "scan", "embeddings", "quality", "models" }));
            Assert.That(snapshot.Entries.Single(entry => entry.Kind == "embeddings").Dimensions,
                Is.EqualTo(2));
            Assert.That(snapshot.Entries.Single(entry => entry.Kind == "embeddings").Entries,
                Is.EqualTo(1));
            Assert.That(snapshot.Entries.Single(entry => entry.Kind == "models").Name,
                Is.EqualTo("model"));
            Assert.That(snapshot.Entries.Single(entry => entry.Kind == "models").Bytes,
                Is.EqualTo(7));
            Assert.That(snapshot.TotalBytes,
                Is.EqualTo(snapshot.Entries.Sum(entry => entry.Bytes)));
        });
    }

    [Test]
    public void CorruptEmbeddingCachesAreIgnored()
    {
        File.WriteAllText(Path.Combine(_cacheDirectory, "embeddings-corrupt.bin"), "not a cache");

        CacheSnapshot snapshot = CacheInspector.Inspect(
            _cacheDirectory,
            Path.Combine(_temp.Root, "models"));

        Assert.That(snapshot.Entries, Is.Empty);
    }

    [Test]
    public void ClearRemovesOnlyCacheFiles()
    {
        string file = _temp.CreateFile("image.jpg", "content");
        ScanCache scan = new(Path.Combine(_cacheDirectory, "scan-cache.json"));
        scan.GetOrCompute("sha256", file, File.ReadAllBytes);
        scan.Save();

        EmbeddingCache embeddings = new(Path.Combine(_cacheDirectory, "embeddings-model.bin"), dimensions: 1);
        embeddings.GetOrCompute(file, _ => new[] { 1.0f });
        embeddings.Save();

        string modelFile = _temp.CreateFile(Path.Combine("models", "model.onnx"), "model");
        string unrelated = _temp.CreateFile(Path.Combine("cache", "notes.txt"), "keep me");

        CacheCleanupResult result = CacheInspector.Clear(_cacheDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(result.Files, Is.EqualTo(2));
            Assert.That(result.Bytes, Is.GreaterThan(0));
            Assert.That(File.Exists(Path.Combine(_cacheDirectory, "scan-cache.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(_cacheDirectory, "embeddings-model.bin")), Is.False);
            Assert.That(File.Exists(modelFile), Is.True);
            Assert.That(File.Exists(unrelated), Is.True);
        });
    }

    [Test]
    public void PruneRemovesMissingEmbeddingEntries()
    {
        string file = _temp.CreateFile("image.jpg", "content");
        EmbeddingCache embeddings = new(Path.Combine(_cacheDirectory, "embeddings-model.bin"), dimensions: 1);
        embeddings.GetOrCompute(file, _ => new[] { 1.0f });
        embeddings.Save();
        File.Delete(file);

        int removed = CacheInspector.Prune(_cacheDirectory);

        Assert.That(removed, Is.EqualTo(1));
        Assert.That(new EmbeddingCache(Path.Combine(_cacheDirectory, "embeddings-model.bin"), 1).Count, Is.Zero);
    }
}
