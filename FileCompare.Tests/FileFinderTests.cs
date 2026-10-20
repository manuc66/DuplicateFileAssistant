namespace FileCompare.Tests;

[TestFixture]
public class FileFinderTests
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
    public void EmptyFileFinderFindsZeroByteFilesRecursively()
    {
        string empty = _temp.CreateFile(Path.Combine("nested", "empty.txt"), string.Empty);
        _temp.CreateFile("content.txt", "content");

        IReadOnlyList<DuplicateFile> result = EmptyFileFinder.Find(
            _temp.Root,
            FileSearchFilter.FromText(0, null, null),
            CancellationToken.None);

        Assert.That(result.Select(file => file.Path), Is.EquivalentTo(new[] { empty }));
    }

    [Test]
    public void BigFileFinderReturnsTheLargestFilesFirst()
    {
        string small = _temp.CreateFile("small.txt", "1");
        string large = _temp.CreateFile("large.txt", "1234567890");
        string medium = _temp.CreateFile("medium.txt", "12345");

        IReadOnlyList<DuplicateFile> result = BigFileFinder.Find(
            _temp.Root,
            2,
            FileSearchFilter.FromText(0, null, null),
            CancellationToken.None);

        Assert.That(result.Select(file => file.Path), Is.EqualTo(new[] { large, medium }));
        Assert.That(result.Select(file => file.Path), Does.Not.Contain(small));
    }

    [Test]
    public void BigFileFinderRespectsAMaximumSize()
    {
        string small = _temp.CreateFile("small.txt", new string('a', 1024));
        string medium = _temp.CreateFile("medium.txt", new string('b', 10 * 1024));
        _temp.CreateFile("large.txt", new string('c', 100 * 1024));

        IReadOnlyList<DuplicateFile> result = BigFileFinder.Find(
            _temp.Root,
            10,
            FileSearchFilter.FromText(0, null, null),
            50 * 1024,
            CancellationToken.None);

        Assert.That(result.Select(file => file.Path), Is.EquivalentTo(new[] { small, medium }));
    }

    [Test]
    public void BigFileFinderRespectsTheMinimumSize()
    {
        _temp.CreateFile("small.txt", new string('a', 1024));
        string medium = _temp.CreateFile("medium.txt", new string('b', 10 * 1024));
        string large = _temp.CreateFile("large.txt", new string('c', 100 * 1024));

        IReadOnlyList<DuplicateFile> result = BigFileFinder.Find(
            _temp.Root,
            10,
            FileSearchFilter.FromText(5 * 1024, null, null),
            CancellationToken.None);

        Assert.That(result.Select(file => file.Path), Is.EquivalentTo(new[] { medium, large }));
    }

    [Test]
    public void TemporaryFileFinderMatchesKnownPatterns()
    {
        string tempFile = _temp.CreateFile("document.tmp", "content");
        string backup = _temp.CreateFile("photo.jpg~", "content");
        string dsStore = _temp.CreateFile(".DS_Store", "content");
        _temp.CreateFile("keep.txt", "content");

        IReadOnlyList<DuplicateFile> result = TemporaryFileFinder.Find(
            _temp.Root,
            FileSearchFilter.FromText(0, null, null),
            CancellationToken.None);

        Assert.That(
            result.Select(file => file.Path),
            Is.EquivalentTo(new[] { tempFile, backup, dsStore }));
    }
}
