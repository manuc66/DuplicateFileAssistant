namespace FileCompare.Tests;

[TestFixture]
public class FileMetadataTests
{
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
