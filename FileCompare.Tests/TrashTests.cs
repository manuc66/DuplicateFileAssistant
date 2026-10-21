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
            Assert.That(trash.GetEntries(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task RestoreAfterRestartUsesThePersistentManifest()
    {
        string source = _temp.CreateFile("photo.jpg", "content");
        string trashFolder = Path.Combine(_temp.Root, "trash");
        Trash firstInstance = new(trashFolder);
        await firstInstance.DeleteAsync(source);

        Trash secondInstance = new(trashFolder);
        TrashEntry entry = secondInstance.GetEntries().Single();

        await secondInstance.RestoreAsync(entry);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(source), Is.True);
            Assert.That(secondInstance.GetEntries(), Is.Empty);
        });
    }

    [Test]
    public async Task RestoreRejectsATamperedTrashFile()
    {
        string source = _temp.CreateFile("photo.jpg", "content");
        Trash trash = new(Path.Combine(_temp.Root, "trash"));
        await trash.DeleteAsync(source);

        TrashEntry entry = trash.GetEntries().Single();
        File.WriteAllText(entry.TrashedPath, "tampered");

        await Assert.ThrowsAsync<InvalidOperationException>(() => trash.RestoreAsync(entry));
        Assert.That(File.Exists(source), Is.False);
    }

    [Test]
    public void CorruptManifestFailsClosed()
    {
        string trashFolder = Path.Combine(_temp.Root, "trash");
        Directory.CreateDirectory(trashFolder);
        File.WriteAllText(Path.Combine(trashFolder, "trash-manifest.json"), "{not valid json");

        Assert.That(() => new Trash(trashFolder), Throws.InvalidOperationException);
    }

    [Test]
    public async Task RestoreRefusesToOverwriteAnExistingOriginal()
    {
        string source = _temp.CreateFile("photo.jpg", "content");
        Trash trash = new(Path.Combine(_temp.Root, "trash"));
        await trash.DeleteAsync(source);
        File.WriteAllText(source, "new file");

        TrashEntry entry = trash.GetEntries().Single();

        await Assert.ThrowsAsync<IOException>(() => trash.RestoreAsync(entry));
        Assert.That(File.ReadAllText(source), Is.EqualTo("new file"));
        Assert.That(File.Exists(entry.TrashedPath), Is.True);
    }

    [Test]
    public async Task RestoreRefusesToOverwriteAnExistingDirectory()
    {
        string source = _temp.CreateFile("photo.jpg", "content");
        Trash trash = new(Path.Combine(_temp.Root, "trash"));
        await trash.DeleteAsync(source);
        Directory.CreateDirectory(source);

        TrashEntry entry = trash.GetEntries().Single();

        await Assert.ThrowsAsync<IOException>(() => trash.RestoreAsync(entry));
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(source), Is.True);
            Assert.That(File.Exists(entry.TrashedPath), Is.True);
            Assert.That(trash.GetEntries(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task DeleteWritesAnOperationJournal()
    {
        string source = _temp.CreateFile("photo.jpg", "content");
        Trash trash = new(Path.Combine(_temp.Root, "trash"));

        await trash.DeleteAsync(source);

        string journal = File.ReadAllText(Path.Combine(trash.TrashFolder, "operations.jsonl"));
        Assert.That(journal, Does.Contain("\"Action\":\"delete\""));
    }

    [Test]
    public async Task DeleteAsync_MovesFileAndReturnsDestination()
    {
        string source = _temp.CreateFile("photo.jpg", "content");
        Trash trash = new(Path.Combine(_temp.Root, "trash"));

        string destination = await trash.DeleteAsync(source);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(source), Is.False);
            Assert.That(File.Exists(destination), Is.True);
        });
    }

    [Test]
    public void Restore_MovesTrashedFileBackToItsOriginalPath()
    {
        string source = _temp.CreateFile("photo.jpg", "content");
        Trash trash = new(Path.Combine(_temp.Root, "trash"));

        string trashed = trash.Delete(source);
        Assert.That(File.Exists(source), Is.False);
        Assert.That(File.Exists(trashed), Is.True);

        trash.Restore(trashed, source);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(source), Is.True);
            Assert.That(File.Exists(trashed), Is.False);
        });
    }

    [Test]
    public void Delete_RejectsAFileChangedSinceAnalysis()
    {
        string source = _temp.CreateFile("photo.jpg", "content");
        FileInfo before = new(source);
        long expectedLength = before.Length;
        DateTime expectedLastWriteTimeUtc = before.LastWriteTimeUtc;

        _temp.CreateFile("photo.jpg", "much longer content");

        Trash trash = new(Path.Combine(_temp.Root, "trash"));

        Assert.That(
            () => trash.Delete(source, expectedLength, expectedLastWriteTimeUtc),
            Throws.InvalidOperationException);
        Assert.That(File.Exists(source), Is.True);
    }

    [Test]
    public void Delete_MovesAFileWhoseMetadataDidNotChange()
    {
        string source = _temp.CreateFile("photo.jpg", "content");
        FileInfo before = new(source);
        Trash trash = new(Path.Combine(_temp.Root, "trash"));

        trash.Delete(source, before.Length, before.LastWriteTimeUtc);

        Assert.That(File.Exists(source), Is.False);
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
