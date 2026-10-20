using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class LastFolderTests
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
    public void UsesTheDefaultWhenNoFileExists()
    {
        string defaultFolder = Path.Combine(_temp.Root, "default");
        Directory.CreateDirectory(defaultFolder);
        string settingsFile = Path.Combine(_temp.Root, "last-folder.json");

        LastFolderStore store = new(settingsFile, new LastFolderSettings(defaultFolder));

        Assert.Multiple(() =>
        {
            Assert.That(store.Current.LastFolder, Is.EqualTo(defaultFolder));
            Assert.That(File.Exists(settingsFile), Is.False);
        });
    }

    [Test]
    public void SavesAndReloadsAnExistingFolder()
    {
        string first = Path.Combine(_temp.Root, "first");
        string second = Path.Combine(_temp.Root, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        string settingsFile = Path.Combine(_temp.Root, "last-folder.json");
        LastFolderStore store = new(settingsFile, new LastFolderSettings(first));

        store.Save(second);

        LastFolderStore reloaded = new(settingsFile, new LastFolderSettings(first));

        Assert.That(reloaded.Current.LastFolder, Is.EqualTo(second));
    }

    [Test]
    public void MissingFolderIsIgnored()
    {
        string defaultFolder = Path.Combine(_temp.Root, "default");
        Directory.CreateDirectory(defaultFolder);
        LastFolderStore store = new(
            Path.Combine(_temp.Root, "last-folder.json"),
            new LastFolderSettings(defaultFolder));
        int changed = 0;
        store.Changed += (_, _) => changed++;

        store.Save(Path.Combine(_temp.Root, "missing"));

        Assert.Multiple(() =>
        {
            Assert.That(store.Current.LastFolder, Is.EqualTo(defaultFolder));
            Assert.That(changed, Is.Zero);
        });
    }

    [Test]
    public void CorruptFileFallsBackToTheDefault()
    {
        string defaultFolder = Path.Combine(_temp.Root, "default");
        Directory.CreateDirectory(defaultFolder);
        string settingsFile = Path.Combine(_temp.Root, "last-folder.json");
        File.WriteAllText(settingsFile, "{not valid json");

        LastFolderStore store = new(settingsFile, new LastFolderSettings(defaultFolder));

        Assert.That(store.Current.LastFolder, Is.EqualTo(defaultFolder));
    }

    [Test]
    public void SaveRaisesChanged()
    {
        string first = Path.Combine(_temp.Root, "first");
        string second = Path.Combine(_temp.Root, "second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        LastFolderStore store = new(
            Path.Combine(_temp.Root, "last-folder.json"),
            new LastFolderSettings(first));
        int changed = 0;
        store.Changed += (_, _) => changed++;

        store.Save(second);

        Assert.That(changed, Is.EqualTo(1));
    }
}
