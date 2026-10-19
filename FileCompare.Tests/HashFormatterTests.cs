using DuplicateAssistant.Business.FileHashing;

namespace FileCompare.Tests;

[TestFixture]
public class HashFormatterTests
{
    [Test]
    public void Format_UsesLowercaseHexadecimal()
    {
        Assert.That(
            HashFormatter.Format(new byte[] { 0x00, 0x0F, 0xFF }),
            Is.EqualTo("000fff"));
    }

    [Test]
    public void Format_AllocatesOnlyTheResultString()
    {
        Span<byte> bytes = stackalloc byte[32];
        bytes.Fill(0xAB);

        _ = HashFormatter.Format(bytes);

        long before = GC.GetAllocatedBytesForCurrentThread();
        string result = HashFormatter.Format(bytes);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Length.EqualTo(64));
            Assert.That(allocated, Is.LessThan(512));
        });
    }
}
