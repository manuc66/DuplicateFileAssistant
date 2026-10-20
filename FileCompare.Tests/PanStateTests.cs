using Avalonia;
using DuplicateAssistant.Business;

namespace FileCompare.Tests;

[TestFixture]
public class PanStateTests
{
    [Test]
    public void MovesByThePointerDelta()
    {
        PanState pan = new();

        pan.Begin(new Point(10, 10), new Vector(100, 100));
        Vector offset = pan.Move(new Point(50, 60), new Vector(800, 600), new Vector(400, 300));

        Assert.That(offset, Is.EqualTo(new Vector(60, 50)));
    }

    [Test]
    public void ClampsToTheScrollableExtent()
    {
        PanState pan = new();

        pan.Begin(new Point(0, 0), new Vector(0, 0));
        Vector offset = pan.Move(new Point(-1000, -1000), new Vector(800, 600), new Vector(400, 300));

        Assert.That(offset, Is.EqualTo(new Vector(400, 300)));
    }

    [Test]
    public void ClampsAtTheOrigin()
    {
        PanState pan = new();

        pan.Begin(new Point(0, 0), new Vector(10, 10));
        Vector offset = pan.Move(new Point(100, 100), new Vector(800, 600), new Vector(400, 300));

        Assert.That(offset, Is.EqualTo(new Vector(0, 0)));
    }
}
