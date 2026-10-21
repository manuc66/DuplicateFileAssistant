using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class FilterSettingsTests
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
    public void MissingKeyReturnsDefaults()
    {
        FilterSettingsStore store = new(Path.Combine(_temp.Root, "filters.json"));

        FilterSettings settings = store.Get("duplicates", FilterSettings.Default);

        Assert.That(settings, Is.EqualTo(FilterSettings.Default));
    }

    [Test]
    public void SavesAndReloadsPerTab()
    {
        string file = Path.Combine(_temp.Root, "filters.json");
        FilterSettingsStore store = new(file);
        FilterSettings big = FilterSettings.Default with
        {
            MinimumSizeText = "10 MB",
            MaximumSizeText = "2 GB",
            Extensions = ".mp4,.mkv"
        };
        FilterSettings similar = FilterSettings.Default with
        {
            UseSemantic = true,
            UseKeeperModel = true,
            SemanticThreshold = 0.9
        };

        store.Save("big-files", big);
        store.Save("similar-images", similar);

        FilterSettingsStore reloaded = new(file);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Get("big-files", FilterSettings.Default), Is.EqualTo(big));
            Assert.That(reloaded.Get("similar-images", FilterSettings.Default), Is.EqualTo(similar));
            Assert.That(reloaded.Get("duplicates", FilterSettings.Default), Is.EqualTo(FilterSettings.Default));
        });
    }

    [Test]
    public void CorruptFileFallsBackToDefaults()
    {
        string file = Path.Combine(_temp.Root, "filters.json");
        File.WriteAllText(file, "{not valid json");
        FilterSettingsStore store = new(file);

        Assert.That(store.Get("duplicates", FilterSettings.Default), Is.EqualTo(FilterSettings.Default));
    }

    [Test]
    public void SaveRaisesChangedOnlyWhenTheValueChanges()
    {
        FilterSettingsStore store = new(Path.Combine(_temp.Root, "filters.json"));
        int changed = 0;
        store.Changed += (_, _) => changed++;

        store.Save("big-files", FilterSettings.Default);
        int changedAfterFirst = changed;
        store.Save("big-files", FilterSettings.Default);
        store.Save("big-files", FilterSettings.Default with { MinimumSizeText = "1 MB" });

        Assert.Multiple(() =>
        {
            Assert.That(changedAfterFirst, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(2));
        });
    }
}
