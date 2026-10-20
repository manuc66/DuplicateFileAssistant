namespace FileCompare.Tests;

[TestFixture]
public class ScanCacheTests
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
    public void CacheHitAvoidsRecomputingTheHash()
    {
        string file = _temp.CreateFile("file.bin", "content");
        string cachePath = Path.Combine(_temp.Root, "cache.json");
        ScanCache cache = new(cachePath);
        int calls = 0;

        byte[] Hash(string path)
        {
            calls++;
            return File.ReadAllBytes(path);
        }

        byte[] first = cache.GetOrCompute("Blake3", file, Hash);
        byte[] second = cache.GetOrCompute("Blake3", file, Hash);

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(calls, Is.EqualTo(1));
        });
    }

    [Test]
    public void ChangingTheFileInvalidatesTheEntry()
    {
        string file = _temp.CreateFile("file.bin", "content");
        string cachePath = Path.Combine(_temp.Root, "cache.json");
        ScanCache cache = new(cachePath);
        int calls = 0;

        byte[] Hash(string path)
        {
            calls++;
            return File.ReadAllBytes(path);
        }

        cache.GetOrCompute("Blake3", file, Hash);
        File.WriteAllText(file, "changed content");
        cache.GetOrCompute("Blake3", file, Hash);

        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void PruneRemovesDeletedFiles()
    {
        string file = _temp.CreateFile("file.bin", "content");
        string cachePath = Path.Combine(_temp.Root, "cache.json");
        ScanCache cache = new(cachePath);
        cache.GetOrCompute("Blake3", file, File.ReadAllBytes);

        File.Delete(file);
        int removed = cache.PruneMissingFiles();

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(cache.Count, Is.Zero);
        });
    }

    [Test]
    public void CacheSurvivesANewInstance()
    {
        string file = _temp.CreateFile("file.bin", "content");
        string cachePath = Path.Combine(_temp.Root, "cache.json");
        ScanCache first = new(cachePath);
        first.GetOrCompute("Blake3", file, File.ReadAllBytes);
        first.Save();

        ScanCache second = new(cachePath);
        int calls = 0;
        byte[] result = second.GetOrCompute("Blake3", file, path =>
        {
            calls++;
            return File.ReadAllBytes(path);
        });

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.Zero);
            Assert.That(result, Is.EqualTo(File.ReadAllBytes(file)));
        });
    }
}
