using DuplicateAssistant.Business;

namespace FileCompare.Tests;

[TestFixture]
public class BoundedTextBufferTests
{
    [Test]
    public void KeepsOnlyTheMostRecentCharacters()
    {
        using BoundedTextBuffer buffer = new(10);

        buffer.Append("0123456789ABCDEF");

        Assert.That(buffer.ToString(), Is.EqualTo("6789ABCDEF"));
        Assert.That(buffer.Length, Is.EqualTo(10));
    }

    [Test]
    public void HandlesWraparound()
    {
        using BoundedTextBuffer buffer = new(5);

        buffer.Append("abc");
        buffer.Append("def");

        Assert.That(buffer.ToString(), Is.EqualTo("bcdef"));
    }

    [Test]
    public void AppendLineKeepsTheMostRecentLines()
    {
        using BoundedTextBuffer buffer = new(10);

        buffer.AppendLine("first");
        buffer.AppendLine("second");

        string expected = $"first{Environment.NewLine}second{Environment.NewLine}";
        Assert.That(buffer.ToString(), Is.EqualTo(expected[^10..]));
    }
}
