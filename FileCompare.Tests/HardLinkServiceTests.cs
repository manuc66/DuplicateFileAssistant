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
}
