namespace FileCompare.Tests;

[TestFixture]
public class SizeDuplicateFinderTests
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
    public void GroupsFilesWithIdenticalSizeRegardlessOfContent()
    {
        string first = _temp.CreateFile("a.txt", "12345");
        string second = _temp.CreateFile("b.txt", "abcde");
        _temp.CreateFile("different.txt", "123");

        Dictionary<HashKey, HashSet<DuplicateFile>> result = SizeDuplicateFinder.Find(
            _temp.Root,
            SearchOption.TopDirectoryOnly,
            FileSearchFilter.FromText(0, null, null),
            TextWriter.Null,
            _ => { },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.Values.Single().Select(file => file.Path), Is.EquivalentTo(new[] { first, second }));
        });
    }
}
