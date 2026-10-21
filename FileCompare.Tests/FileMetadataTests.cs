namespace FileCompare.Tests;

[TestFixture]
public class FileMetadataTests
{
    [Test]
    public void ApplyRestoresTimestampAndAttributes()
    {
        using TempDirectory temp = new();
        string destination = temp.CreateFile("destination.txt", "content");
        DateTime timestamp = new(2021, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        TrashEntry entry = new(
            "trashed.txt",
            destination,
            7,
            timestamp,
            timestamp,
            "hash",
            FileAttributes.Normal,
            null,
            DateTime.UtcNow,
            IsSymbolicLink: false);

        FileMetadata.Apply(entry, destination);

        Assert.That(File.GetLastWriteTimeUtc(destination), Is.EqualTo(timestamp));
    }

    [Test]
    public void CopyPreservesLastWriteTime()
    {
        using TempDirectory temp = new();
        string source = temp.CreateFile("source.txt", "content");
        string destination = temp.CreateFile("destination.txt", "other");
        DateTime timestamp = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(source, timestamp);

        FileMetadata.Copy(new FileInfo(source), destination);

        Assert.That(File.GetLastWriteTimeUtc(destination), Is.EqualTo(timestamp));
    }
}
