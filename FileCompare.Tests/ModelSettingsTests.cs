using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class ModelSettingsTests
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
        string settingsFile = Path.Combine(_temp.Root, "model-settings.json");
        string folder = Path.Combine(_temp.Root, "models");
        ModelSettingsStore store = new(settingsFile, new ModelSettings(folder));

        Assert.Multiple(() =>
        {
            Assert.That(store.Current.ModelsFolder, Is.EqualTo(folder));
            Assert.That(File.Exists(settingsFile), Is.False);
        });
    }

    [Test]
    public void SavesAndReloadsTheFolder()
    {
        string settingsFile = Path.Combine(_temp.Root, "model-settings.json");
        string firstFolder = Path.Combine(_temp.Root, "models-a");
        string secondFolder = Path.Combine(_temp.Root, "models-b");
        ModelSettingsStore first = new(settingsFile, new ModelSettings(firstFolder));

        first.Save(new ModelSettings(secondFolder));

        ModelSettingsStore second = new(settingsFile, new ModelSettings(firstFolder));

        Assert.That(second.Current.ModelsFolder, Is.EqualTo(secondFolder));
    }

    [Test]
    public void CorruptSettingsFallBackToDefaults()
    {
        string settingsFile = Path.Combine(_temp.Root, "model-settings.json");
        File.WriteAllText(settingsFile, "{not valid json");
        ModelSettings defaults = new(Path.Combine(_temp.Root, "models"));

        ModelSettingsStore store = new(settingsFile, defaults);

        Assert.That(store.Current, Is.EqualTo(defaults));
    }

    [Test]
    public void NormalizeFolderRejectsTheFilesystemRoot()
    {
        string root = Path.GetPathRoot(Path.GetFullPath(_temp.Root)) ?? "/";

        Assert.That(() => ModelSettingsStore.NormalizeFolder(root), Throws.ArgumentException);
    }

    [Test]
    public void NormalizeFolderRejectsAFile()
    {
        string file = _temp.CreateFile("not-a-folder", "content");

        Assert.That(
            () => ModelSettingsStore.NormalizeFolder(file),
            Throws.ArgumentException.With.Message.Contain("cannot be a file"));
    }

    [Test]
    public void SettingsThatPointToAFileFallBackToTheDefault()
    {
        string settingsFile = Path.Combine(_temp.Root, "model-settings.json");
        string file = _temp.CreateFile("not-a-folder", "content");
        string defaultFolder = Path.Combine(_temp.Root, "models");
        File.WriteAllText(settingsFile, System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<string, string> { ["ModelsFolder"] = file }));

        ModelSettingsStore store = new(settingsFile, new ModelSettings(defaultFolder));

        Assert.That(store.Current.ModelsFolder, Is.EqualTo(defaultFolder));
    }

    [Test]
    public void SaveRaisesChanged()
    {
        string settingsFile = Path.Combine(_temp.Root, "model-settings.json");
        ModelSettingsStore store = new(settingsFile, new ModelSettings(Path.Combine(_temp.Root, "models")));
        int changed = 0;
        store.Changed += (_, _) => changed++;

        store.Save(new ModelSettings(Path.Combine(_temp.Root, "other-models")));

        Assert.That(changed, Is.EqualTo(1));
    }
}
