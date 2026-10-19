using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class TrashTests
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
    public void Delete_CreatesTrashFolderAndMovesFile()
    {
        string source = _temp.CreateFile("photo.jpg", "content");
        string trashFolder = Path.Combine(_temp.Root, "trash");
        Trash trash = new(trashFolder);

        trash.Delete(source);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(source), Is.False);
            Assert.That(Directory.Exists(trashFolder), Is.True);
            Assert.That(Directory.GetFiles(trashFolder), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void Constructor_ExpandsTildeAndCreatesFolder()
    {
        string folderName = "DuplicateAssistantTests-" + Guid.NewGuid().ToString("N");
        string configured = Path.Combine("~", folderName);
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            folderName);

        try
        {
            Trash trash = new(configured);

            Assert.Multiple(() =>
            {
                Assert.That(trash.TrashFolder, Is.EqualTo(expected));
                Assert.That(Directory.Exists(expected), Is.True);
            });
        }
        finally
        {
            if (Directory.Exists(expected))
            {
                Directory.Delete(expected, recursive: true);
            }
        }
    }
}
