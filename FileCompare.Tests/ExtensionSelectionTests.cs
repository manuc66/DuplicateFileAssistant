using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class ExtensionSelectionTests
{
    [Test]
    public void EmptySelectionMeansAllFiles()
    {
        ExtensionSelectionViewModel selection = new();

        Assert.Multiple(() =>
        {
            Assert.That(selection.Summary, Is.EqualTo("All files"));
            Assert.That(selection.ToFilterString(), Is.Empty);
            Assert.That(selection.Options.All(option => !option.IsSelected), Is.True);
        });
    }

    [Test]
    public void ParsesKnownAndCustomExtensions()
    {
        ExtensionSelectionViewModel selection = new();

        selection.SetFromFilterString("jpg, .PNG; pdf, .xyz");

        Assert.Multiple(() =>
        {
            Assert.That(selection.Options.Single(option => option.Extension == ".jpg").IsSelected, Is.True);
            Assert.That(selection.Options.Single(option => option.Extension == ".png").IsSelected, Is.True);
            Assert.That(selection.Options.Single(option => option.Extension == ".pdf").IsSelected, Is.True);
            Assert.That(selection.Options.Single(option => option.Extension == ".xyz").IsSelected, Is.True);
            Assert.That(selection.ToFilterString(), Is.EqualTo(".jpg,.png,.pdf,.xyz"));
            Assert.That(selection.Summary, Does.Contain("4"));
        });
    }

    [Test]
    public void TogglingAnOptionUpdatesTheFilterAndSummary()
    {
        ExtensionSelectionViewModel selection = new();
        ExtensionOption jpg = selection.Options.Single(option => option.Extension == ".jpg");

        jpg.IsSelected = true;

        Assert.Multiple(() =>
        {
            Assert.That(selection.ToFilterString(), Is.EqualTo(".jpg"));
            Assert.That(selection.Summary, Does.Contain("1"));
        });

        jpg.IsSelected = false;

        Assert.Multiple(() =>
        {
            Assert.That(selection.ToFilterString(), Is.Empty);
            Assert.That(selection.Summary, Is.EqualTo("All files"));
        });
    }

    [Test]
    public void ClearReturnsToAllFiles()
    {
        ExtensionSelectionViewModel selection = new();
        selection.SetFromFilterString("jpg,png");

        selection.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(selection.ToFilterString(), Is.Empty);
            Assert.That(selection.Summary, Is.EqualTo("All files"));
        });
    }
}
