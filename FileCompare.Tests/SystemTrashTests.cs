using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class SystemTrashTests
{
    [Test]
    [NonParallelizable]
    public async Task XdgTrashMovesRestoresAndRemovesItsMetadata()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("The XDG backend is Linux-only.");
            return;
        }

        using TempDirectory temp = new();
        string? previousDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(temp.Root, "data"));

        try
        {
            SystemTrash trash = new(Path.Combine(temp.Root, "manifest"), new XdgTrashBackend());
            string source = temp.CreateFile("photo.jpg", "content");

            await trash.DeleteAsync(source, expectedLength: null, expectedLastWriteTimeUtc: null,
                CancellationToken.None);

            TrashEntry entry = trash.GetEntries().Single();

            SystemTrash restarted = new(Path.Combine(temp.Root, "manifest"), new XdgTrashBackend());
            TrashEntry reloaded = restarted.GetEntries().Single();

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(source), Is.False);
                Assert.That(entry.Backend, Is.EqualTo(TrashBackend.System));
                Assert.That(File.Exists(entry.TrashedPath), Is.True);
                Assert.That(entry.SystemMetadataPath, Is.Not.Null);
                Assert.That(File.Exists(entry.SystemMetadataPath), Is.True);
                Assert.That(reloaded.Backend, Is.EqualTo(TrashBackend.System));
                Assert.That(reloaded.SystemMetadataPath, Is.EqualTo(entry.SystemMetadataPath));
            });

            string info = File.ReadAllText(entry.SystemMetadataPath!);
            Assert.Multiple(() =>
            {
                Assert.That(info, Does.Contain("[Trash Info]"));
                Assert.That(info, Does.Contain("Path="));
                Assert.That(info, Does.Contain("DeletionDate="));
            });

            await restarted.RestoreAsync(reloaded);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(source), Is.True);
                Assert.That(restarted.GetEntries(), Is.Empty);
                Assert.That(File.Exists(entry.SystemMetadataPath), Is.False);
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", previousDataHome);
        }
    }

    [Test]
    [NonParallelizable]
    public async Task XdgTrashDiscardDeletesTheFileAndMetadata()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("The XDG backend is Linux-only.");
            return;
        }

        using TempDirectory temp = new();
        string? previousDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(temp.Root, "data"));

        try
        {
            SystemTrash trash = new(Path.Combine(temp.Root, "manifest"), new XdgTrashBackend());
            string source = temp.CreateFile("photo.jpg", "content");
            await trash.DeleteAsync(source, expectedLength: null, expectedLastWriteTimeUtc: null,
                CancellationToken.None);

            TrashEntry entry = trash.GetEntries().Single();
            trash.Discard(entry);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(entry.TrashedPath), Is.False);
                Assert.That(File.Exists(entry.SystemMetadataPath), Is.False);
                Assert.That(trash.GetEntries(), Is.Empty);
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", previousDataHome);
        }
    }
}
