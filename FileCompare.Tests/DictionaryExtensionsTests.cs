using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class DictionaryExtensionsTests
{
    [Test]
    public void AddOrUpdateAddsANewKey()
    {
        Dictionary<string, int> values = new();

        int result = values.AddOrUpdate("a", 1, (_, existing) => existing + 1);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(1));
            Assert.That(values["a"], Is.EqualTo(1));
        });
    }

    [Test]
    public void AddOrUpdateUpdatesAnExistingKey()
    {
        Dictionary<string, int> values = new() { ["a"] = 1 };

        int result = values.AddOrUpdate("a", 99, (_, existing) => existing + 1);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(2));
            Assert.That(values["a"], Is.EqualTo(2));
        });
    }

    [Test]
    public void AddOrUpdateActionOverloadUpdatesInPlace()
    {
        Dictionary<string, List<int>> values = new();

        values.AddOrUpdate("a", new List<int> { 1 }, existing => existing.Add(2));
        Assert.That(values["a"], Is.EqualTo(new[] { 1 }));

        List<int> updated = values.AddOrUpdate("a", new List<int> { 9 }, existing => existing.Add(3));
        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.SameAs(values["a"]));
            Assert.That(values["a"], Is.EqualTo(new[] { 1, 3 }));
        });
    }

    [Test]
    public void AddOrUpdateFactoriesAddThenUpdate()
    {
        Dictionary<string, int> values = new();
        int addCalls = 0;
        int updateCalls = 0;

        int added = values.AddOrUpdate(
            "a",
            key =>
            {
                addCalls++;
                return key.Length;
            },
            (_, existing) =>
            {
                updateCalls++;
                return existing + 10;
            });

        int updated = values.AddOrUpdate(
            "a",
            _ => 100,
            (_, existing) =>
            {
                updateCalls++;
                return existing + 10;
            });

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.EqualTo(1));
            Assert.That(updated, Is.EqualTo(11));
            Assert.That(addCalls, Is.EqualTo(1));
            Assert.That(updateCalls, Is.EqualTo(1));
        });
    }
}

[TestFixture]
public class FileExtTests
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
    public void FilesAreEqualComparesTheContent()
    {
        string first = _temp.CreateFile("first.bin", "same content");
        string second = _temp.CreateFile("second.bin", "same content");
        string different = _temp.CreateFile("different.bin", "other content");

        Assert.Multiple(() =>
        {
            Assert.That(FileExt.FilesAreEqual(new FileInfo(first), new FileInfo(second)), Is.True);
            Assert.That(FileExt.FilesAreEqual(new FileInfo(first), new FileInfo(different)), Is.False);
        });
    }
}
