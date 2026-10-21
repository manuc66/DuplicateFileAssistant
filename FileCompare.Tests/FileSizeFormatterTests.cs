using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class FileSizeFormatterTests
{
    [TestCase(0, "0 B")]
    [TestCase(1, "1 B")]
    [TestCase(512, "512 B")]
    [TestCase(1023, "1023 B")]
    [TestCase(1024, "1 KB")]
    [TestCase(1536, "1.5 KB")]
    [TestCase(10 * 1024, "10 KB")]
    [TestCase(1024 * 1024, "1 MB")]
    [TestCase(1024 * 1024 + 512 * 1024, "1.5 MB")]
    [TestCase(100L * 1024 * 1024, "100 MB")]
    [TestCase(1024L * 1024 * 1024, "1 GB")]
    [TestCase(3L * 1024 * 1024 * 1024, "3 GB")]
    public void FormatsBytesWithHumanUnits(long bytes, string expected)
    {
        Assert.That(FileSizeFormatter.Format(bytes), Is.EqualTo(expected));
    }

    [Test]
    public void NegativeValuesAreClampedToZero()
    {
        Assert.That(FileSizeFormatter.Format(-42), Is.EqualTo("0 B"));
    }
}
