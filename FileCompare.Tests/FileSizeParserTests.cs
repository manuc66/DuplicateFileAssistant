using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class FileSizeParserTests
{
    [TestCase("0", 0)]
    [TestCase("1024", 1024)]
    [TestCase("1 KB", 1024)]
    [TestCase("1.5 KB", 1536)]
    [TestCase("10MB", 10L * 1024 * 1024)]
    [TestCase("1.5 GB", 1610612736L)]
    [TestCase(" 2 mb ", 2L * 1024 * 1024)]
    [TestCase("1,5 MB", 1572864L)]
    public void ParsesHumanReadableSizes(string text, long expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileSizeParser.TryParse(text, out long bytes), Is.True);
            Assert.That(bytes, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("abc")]
    [TestCase("10 XB")]
    [TestCase("-1 MB")]
    public void RejectsInvalidSizes(string text)
    {
        Assert.That(FileSizeParser.TryParse(text, out _), Is.False);
    }
}
