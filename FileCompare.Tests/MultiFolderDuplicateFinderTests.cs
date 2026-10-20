namespace FileCompare.Tests;

[TestFixture]
public class MultiFolderDuplicateFinderTests
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
    public void FindsDuplicatesAcrossSeveralFolders()
    {
        string firstRoot = Path.Combine(_temp.Root, "first");
        string secondRoot = Path.Combine(_temp.Root, "second");
        string thirdRoot = Path.Combine(_temp.Root, "third");
        string first = _temp.CreateFile(Path.Combine("first", "a.txt"), "same");
        string second = _temp.CreateFile(Path.Combine("second", "b.txt"), "same");
        _temp.CreateFile(Path.Combine("third", "c.txt"), "same");
        _temp.CreateFile(Path.Combine("first", "unique.txt"), "unique");

        Dictionary<HashKey, HashSet<DuplicateFile>> result = MultiFolderDuplicateFinder.Find(
            new[] { firstRoot, secondRoot, thirdRoot },
            SearchOption.AllDirectories,
            FileSearchFilter.FromText(0, null, null),
            File.ReadAllBytes,
            TextWriter.Null,
            _ => { },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.Values.Single().Select(file => file.Path), Does.Contain(first));
            Assert.That(result.Values.Single().Select(file => file.Path), Does.Contain(second));
        });
    }
}
