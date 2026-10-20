using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class CommonFileInfoTests
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
    public void ReportsTheLengthAndDoesNotTreatRegularFilesAsLinksOrFifos()
    {
        string path = _temp.CreateFile("file.bin", "12345");
        CommonFileInfo info = new(path);

        Assert.Multiple(() =>
        {
            Assert.That(info.Length, Is.EqualTo(5));
            Assert.That(info.IsLink(), Is.False);
            Assert.That(info.IsFifoFile(), Is.False);
        });
    }

    [Test]
    public void DetectsSymbolicLinks()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Symbolic link creation is tested on Linux.");
        }

        string target = _temp.CreateFile("target.txt", "content");
        string link = Path.Combine(_temp.Root, "link.txt");
        File.CreateSymbolicLink(link, target);

        CommonFileInfo info = new(link);

        Assert.That(info.IsLink(), Is.True);
    }
}
