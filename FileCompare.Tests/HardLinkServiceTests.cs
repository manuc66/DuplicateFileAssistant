namespace FileCompare.Tests;

[TestFixture]
public class HardLinkServiceTests
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
    public void CreatesASecondDirectoryEntryForTheSameFile()
    {
        string original = _temp.CreateFile("original.txt", "content");
        string link = Path.Combine(_temp.Root, "link.txt");

        HardLinkService.Create(link, original);

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(link), Is.EqualTo("content"));
            Assert.That(FileIdentity.TryGet(original, out FileIdentity first), Is.True);
            Assert.That(FileIdentity.TryGet(link, out FileIdentity second), Is.True);
            Assert.That(first, Is.EqualTo(second));
        });
    }

    [Test]
    public void RejectsAMissingTarget()
    {
        string missing = Path.Combine(_temp.Root, "missing.txt");

        Assert.That(
            () => HardLinkService.Create(Path.Combine(_temp.Root, "link.txt"), missing),
            Throws.TypeOf<FileNotFoundException>());
    }

    [Test]
    public void RejectsAnExistingDestination()
    {
        string original = _temp.CreateFile("original.txt", "content");
        string existing = _temp.CreateFile("existing.txt", "other");

        Assert.That(
            () => HardLinkService.Create(existing, original),
            Throws.TypeOf<IOException>());
    }

    [Test]
    public void FileIdentityReportsMissingPaths()
    {
        Assert.That(
            FileIdentity.TryGet(Path.Combine(_temp.Root, "missing.txt"), out _),
            Is.False);
    }
}
