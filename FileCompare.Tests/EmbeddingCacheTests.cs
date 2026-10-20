using System.Text;
using ImageCompare;

namespace FileCompare.Tests;

[TestFixture]
public class EmbeddingCacheTests
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
    public void ATrucatedCacheIsIgnored()
    {
        string cachePath = _temp.CreateFile("cache.bin", new byte[] { 1, 2, 3, 4, 5 });
        string file = _temp.CreateFile("image.jpg", "content");

        EmbeddingCache cache = new(cachePath, dimensions: 2);
        int calls = 0;
        float[] vector = cache.GetOrCompute(file, _ =>
        {
            calls++;
            return new[] { 0.5f, 0.25f };
        });

        Assert.Multiple(() =>
        {
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(vector, Is.EqualTo(new[] { 0.5f, 0.25f }));
        });
    }

    [Test]
    public void ACacheWithAnInvalidTimestampIsIgnored()
    {
        string cachePath = Path.Combine(_temp.Root, "cache.bin");

        using (FileStream stream = File.Create(cachePath))
        using (BinaryWriter writer = new(stream, Encoding.UTF8))
        {
            writer.Write(0x4943454D); // Magic
            writer.Write(1); // Version
            writer.Write(1); // Dimensions
            writer.Write(1); // Entry count
            writer.Write("file.jpg");
            writer.Write(1L);
            writer.Write(long.MaxValue); // Invalid ticks
            writer.Write(0.5f);
        }

        EmbeddingCache cache = new(cachePath, dimensions: 1);

        Assert.That(cache.Count, Is.Zero);
    }

    [Test]
    public void ACacheForAnotherDimensionIsIgnored()
    {
        string file = _temp.CreateFile("image.jpg", "content");
        string cachePath = Path.Combine(_temp.Root, "cache.bin");
        EmbeddingCache original = new(cachePath, dimensions: 2);
        original.GetOrCompute(file, _ => new[] { 0.1f, 0.2f });
        original.Save();

        EmbeddingCache reloaded = new(cachePath, dimensions: 3);

        Assert.That(reloaded.Count, Is.Zero);
    }

    [Test]
    public void TryReadHeaderRejectsCorruptFiles()
    {
        string cachePath = _temp.CreateFile("cache.bin", "not a cache");

        Assert.That(EmbeddingCache.TryReadHeader(cachePath, out _, out _), Is.False);
    }

    [Test]
    public void GetOrComputeRejectsAVectorOfTheWrongSize()
    {
        string file = _temp.CreateFile("image.jpg", "content");
        EmbeddingCache cache = new(Path.Combine(_temp.Root, "cache.bin"), dimensions: 2);

        Assert.That(
            () => cache.GetOrCompute(file, _ => new[] { 0.1f }),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void SavingIsAtomicAndReloads()
    {
        string file = _temp.CreateFile("image.jpg", "content");
        string cachePath = Path.Combine(_temp.Root, "cache", "embeddings.bin");
        EmbeddingCache cache = new(cachePath, dimensions: 2);
        cache.GetOrCompute(file, _ => new[] { 0.1f, 0.2f });

        cache.Save();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(cachePath), Is.True);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(cachePath)!, "*.tmp"), Is.Empty);
            Assert.That(new EmbeddingCache(cachePath, dimensions: 2).Count, Is.EqualTo(1));
        });
    }
}
