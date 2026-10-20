using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class TrashSettingsTests
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
    public void UsesDefaultsWhenNoUserSettingsExist()
    {
        string settingsFile = Path.Combine(_temp.Root, "settings.json");
        TrashSettings defaults = new(TrashMode.Application, Path.Combine(_temp.Root, "trash"));

        TrashSettingsStore store = new(settingsFile, defaults);

        Assert.Multiple(() =>
        {
            Assert.That(store.Current.Mode, Is.EqualTo(TrashMode.Application));
            Assert.That(store.Current.TrashFolder, Is.EqualTo(defaults.TrashFolder));
            Assert.That(File.Exists(settingsFile), Is.False);
        });
    }

    [Test]
    public void SavesAndReloadsTheChoice()
    {
        string settingsFile = Path.Combine(_temp.Root, "settings.json");
        string folder = Path.Combine(_temp.Root, "custom-trash");
        TrashSettingsStore first = new(settingsFile, new TrashSettings(TrashMode.Application, folder));

        first.Save(new TrashSettings(TrashMode.System, folder));

        TrashSettingsStore second = new(settingsFile, new TrashSettings(TrashMode.Application, folder));

        Assert.Multiple(() =>
        {
            Assert.That(second.Current.Mode, Is.EqualTo(TrashMode.System));
            Assert.That(second.Current.TrashFolder, Is.EqualTo(folder));
        });
    }

    [Test]
    public void CorruptSettingsFallBackToDefaults()
    {
        string settingsFile = Path.Combine(_temp.Root, "settings.json");
        File.WriteAllText(settingsFile, "{not valid json");
        TrashSettings defaults = new(TrashMode.Application, Path.Combine(_temp.Root, "trash"));

        TrashSettingsStore store = new(settingsFile, defaults);

        Assert.That(store.Current, Is.EqualTo(defaults));
    }

    [Test]
    public void ParseModeIsCaseInsensitiveAndFallsBackToApplication()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TrashSettingsStore.ParseMode("System"), Is.EqualTo(TrashMode.System));
            Assert.That(TrashSettingsStore.ParseMode("system"), Is.EqualTo(TrashMode.System));
            Assert.That(TrashSettingsStore.ParseMode("nonsense"), Is.EqualTo(TrashMode.Application));
            Assert.That(TrashSettingsStore.ParseMode(null), Is.EqualTo(TrashMode.Application));
        });
    }

    [Test]
    public void NormalizeFolderRejectsTheFilesystemRoot()
    {
        string root = Path.GetPathRoot(Path.GetFullPath(_temp.Root)) ?? "/";

        Assert.That(() => TrashSettingsStore.NormalizeFolder(root), Throws.ArgumentException);
    }

    [Test]
    public void NormalizeFolderRejectsAFile()
    {
        string file = _temp.CreateFile("not-a-folder", "content");

        Assert.That(
            () => TrashSettingsStore.NormalizeFolder(file),
            Throws.ArgumentException.With.Message.Contain("cannot be a file"));
    }

    [Test]
    public void SettingsThatPointToAFileFallBackToTheDefault()
    {
        string settingsFile = Path.Combine(_temp.Root, "settings.json");
        string file = _temp.CreateFile("not-a-folder", "content");
        string defaultFolder = Path.Combine(_temp.Root, "trash");
        File.WriteAllText(settingsFile, System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<string, string>
            {
                ["Mode"] = "Application",
                ["TrashFolder"] = file
            }));

        TrashSettingsStore store = new(
            settingsFile,
            new TrashSettings(TrashMode.Application, defaultFolder));

        Assert.That(store.Current.TrashFolder, Is.EqualTo(defaultFolder));
    }

    [Test]
    public void SaveRaisesChanged()
    {
        string settingsFile = Path.Combine(_temp.Root, "settings.json");
        TrashSettingsStore store = new(settingsFile,
            new TrashSettings(TrashMode.Application, Path.Combine(_temp.Root, "trash")));
        int changed = 0;
        store.Changed += (_, _) => changed++;

        store.Save(new TrashSettings(TrashMode.System, Path.Combine(_temp.Root, "other")));

        Assert.That(changed, Is.EqualTo(1));
    }
}
