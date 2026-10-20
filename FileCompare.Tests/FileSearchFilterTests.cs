namespace FileCompare.Tests;

[TestFixture]
public class FileSearchFilterTests
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
    public void NoFiltersMatchEveryPathAndFile()
    {
        FileSearchFilter filter = FileSearchFilter.FromText(0, null, null);
        string path = _temp.CreateFile("photo.jpg", "content");

        Assert.Multiple(() =>
        {
            Assert.That(filter.MatchesPath(path), Is.True);
            Assert.That(filter.MatchesFile(CustomFileInfo.Create(path)), Is.True);
        });
    }

    [Test]
    public void ExtensionFilterIsCaseInsensitive()
    {
        FileSearchFilter filter = FileSearchFilter.FromText(0, "jpg; .PNG", null);
        string jpg = _temp.CreateFile("photo.JPG", "content");
        string png = _temp.CreateFile("image.png", "content");
        string text = _temp.CreateFile("notes.txt", "content");

        Assert.Multiple(() =>
        {
            Assert.That(filter.MatchesPath(jpg), Is.True);
            Assert.That(filter.MatchesPath(png), Is.True);
            Assert.That(filter.MatchesPath(text), Is.False);
        });
    }

    [Test]
    public void MinimumSizeFilterRejectsSmallFiles()
    {
        FileSearchFilter filter = FileSearchFilter.FromText(10, null, null);
        string small = _temp.CreateFile("small.jpg", "12345");
        string large = _temp.CreateFile("large.jpg", "12345678901");

        Assert.Multiple(() =>
        {
            Assert.That(filter.MatchesFile(CustomFileInfo.Create(small)), Is.False);
            Assert.That(filter.MatchesFile(CustomFileInfo.Create(large)), Is.True);
        });
    }

    [Test]
    public void ExcludedFolderFilterRejectsItsChildren()
    {
        string excludedDirectory = Path.Combine(_temp.Root, "excluded");
        FileSearchFilter filter = FileSearchFilter.FromText(0, null, excludedDirectory);

        string included = _temp.CreateFile(Path.Combine("included", "photo.jpg"), "content");
        string excluded = _temp.CreateFile(Path.Combine("excluded", "photo.jpg"), "content");

        Assert.Multiple(() =>
        {
            Assert.That(filter.MatchesPath(included), Is.True);
            Assert.That(filter.MatchesPath(excluded), Is.False);
        });
    }

    [Test]
    public void DupProviderRespectsThePathFilter()
    {
        string includedDirectory = Path.Combine(_temp.Root, "included");
        string excludedDirectory = Path.Combine(_temp.Root, "excluded");
        _temp.CreateFile(Path.Combine("included", "a1.txt"), "same");
        _temp.CreateFile(Path.Combine("included", "a2.txt"), "same");
        _temp.CreateFile(Path.Combine("excluded", "b1.txt"), "same");
        _temp.CreateFile(Path.Combine("excluded", "b2.txt"), "same");

        Dictionary<HashKey, HashSet<DuplicateFile>> result = DupProvider.FindDuplicateByHash(
            _temp.Root,
            SearchOption.AllDirectories,
            _ => true,
            TextWriter.Null,
            _ => { },
            File.ReadAllBytes,
            CancellationToken.None,
            path => !path.StartsWith(excludedDirectory, StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(
                result.Values.Single().Select(file => file.Path),
                Is.All.StartsWith(includedDirectory));
        });
    }
}
