using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class AtomicFileTests
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
    public void WriteAllTextCreatesMissingDirectories()
    {
        string path = Path.Combine(_temp.Root, "nested", "settings.json");

        AtomicFile.WriteAllText(path, "{\"ok\":true}");

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(path), Is.EqualTo("{\"ok\":true}"));
            Assert.That(Directory.GetFiles(_temp.Root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        });
    }

    [Test]
    public void WriteAllTextReplacesAnExistingFile()
    {
        string path = _temp.CreateFile("settings.json", "old");

        AtomicFile.WriteAllText(path, "new");

        Assert.That(File.ReadAllText(path), Is.EqualTo("new"));
    }

    [Test]
    public void AThrowingWriterKeepsTheOriginalFileAndLeavesNoTemporaryFile()
    {
        string path = _temp.CreateFile("settings.json", "original");

        Assert.That(
            () => AtomicFile.Write(path, _ => throw new InvalidOperationException("boom")),
            Throws.InvalidOperationException);

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(path), Is.EqualTo("original"));
            Assert.That(Directory.GetFiles(_temp.Root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        });
    }

    [Test]
    public void AThrowingWriterDoesNotCreateTheDestination()
    {
        string path = Path.Combine(_temp.Root, "missing.json");

        Assert.That(
            () => AtomicFile.Write(path, _ => throw new InvalidOperationException("boom")),
            Throws.InvalidOperationException);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(path), Is.False);
            Assert.That(Directory.GetFiles(_temp.Root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        });
    }

    [Test]
    public void WriteAllBytesStoresTheExactPayload()
    {
        string path = Path.Combine(_temp.Root, "cache.bin");
        byte[] payload = { 0, 1, 2, 255 };

        AtomicFile.WriteAllBytes(path, payload);

        Assert.That(File.ReadAllBytes(path), Is.EqualTo(payload));
    }
}
