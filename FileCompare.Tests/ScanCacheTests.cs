using ImageCompare;

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
    public void CacheInspectorPrunesMissingFiles()
    {
        string file = _temp.CreateFile("file.bin", "content");
        string directory = Path.Combine(_temp.Root, "cache");
        Directory.CreateDirectory(directory);
        string cachePath = Path.Combine(directory, "scan-cache.json");
        ScanCache cache = new(cachePath);
        cache.GetOrCompute("Blake3", file, File.ReadAllBytes);
        cache.Save();

        File.Delete(file);
        int removed = CacheInspector.Prune(directory);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(new ScanCache(cachePath).Count, Is.Zero);
        });
    }

    [Test]
    public void CorruptJsonFallsBackToAnEmptyCache()
    {
        string cachePath = Path.Combine(_temp.Root, "cache.json");
        File.WriteAllText(cachePath, "{not valid json");
        string file = _temp.CreateFile("file.bin", "content");

        ScanCache cache = new(cachePath);
        int calls = 0;
        cache.GetOrCompute("Blake3", file, path =>
        {
            calls++;
            return File.ReadAllBytes(path);
        });

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(cache.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void NullEntriesInTheCacheAreIgnored()
    {
        string cachePath = Path.Combine(_temp.Root, "cache.json");
        string file = _temp.CreateFile("file.bin", "content");
        File.WriteAllText(cachePath,
            System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?> { [file] = null }));

        ScanCache cache = new(cachePath);
        int calls = 0;
        byte[] result = cache.GetOrCompute("Blake3", file, path =>
        {
            calls++;
            return File.ReadAllBytes(path);
        });

        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(result, Is.EqualTo(File.ReadAllBytes(file)));
        });
    }

    [Test]
    public void AnEntryWithoutAHashIsRecomputed()
    {
        string cachePath = Path.Combine(_temp.Root, "cache.json");
        string file = _temp.CreateFile("file.bin", "content");
        FileInfo info = new(file);
        string json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            [file] = new
            {
                Algorithm = "Blake3",
                Length = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                Hash = (string?)null
            }
        });
        File.WriteAllText(cachePath, json);

        ScanCache cache = new(cachePath);
        int calls = 0;
        cache.GetOrCompute("Blake3", file, path =>
        {
            calls++;
            return File.ReadAllBytes(path);
        });

        // The unusable entry is dropped at load time, so the file is hashed again.
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void AnEntryWithInvalidHexIsRecomputed()
    {
        string cachePath = Path.Combine(_temp.Root, "cache.json");
        string file = _temp.CreateFile("file.bin", "content");
        FileInfo info = new(file);
        string json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [file] = new
            {
                Algorithm = "Blake3",
                Length = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                Hash = "not-hex"
            }
        });
        File.WriteAllText(cachePath, json);

        ScanCache cache = new(cachePath);
        int calls = 0;
        cache.GetOrCompute("Blake3", file, path =>
        {
            calls++;
            return File.ReadAllBytes(path);
        });

        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void PathsAreComparedWithThePlatformRules()
    {
        string file = _temp.CreateFile("file.bin", "content");
        string cachePath = Path.Combine(_temp.Root, "cache.json");
        ScanCache cache = new(cachePath);
        cache.GetOrCompute("Blake3", file, File.ReadAllBytes);

        string alternateCase = file.ToUpperInvariant();

        if (!PathUtilities.PathComparer.Equals(file, alternateCase))
        {
            Assert.Ignore("The filesystem is case-sensitive on this platform.");
        }

        int calls = 0;
        cache.GetOrCompute("Blake3", alternateCase, path =>
        {
            calls++;
            return File.ReadAllBytes(path);
        });

        Assert.That(calls, Is.Zero);
    }
}

