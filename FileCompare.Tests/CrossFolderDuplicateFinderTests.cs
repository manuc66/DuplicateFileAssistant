namespace FileCompare.Tests;

[TestFixture]
public class CrossFolderDuplicateFinderTests
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
    public void FindsSourceFilesThatExistElsewhere()
    {
        string source = Path.Combine(_temp.Root, "source");
        string other = Path.Combine(_temp.Root, "other");
        string sourceFile = _temp.CreateFile(Path.Combine("source", "a.txt"), "same");
        string otherFile = _temp.CreateFile(Path.Combine("other", "b.txt"), "same");
        _temp.CreateFile(Path.Combine("source", "different.txt"), "source only");
        _temp.CreateFile(Path.Combine("other", "different.txt"), "other content");

        IReadOnlyList<CrossFolderDuplicate> result = CrossFolderDuplicateFinder.Find(
            source,
            other,
            _ => true,
            File.ReadAllBytes,
            TextWriter.Null,
            _ => { },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Source.Path, Is.EqualTo(sourceFile));
            Assert.That(result[0].Other.Path, Is.EqualTo(otherFile));
        });
    }

    [Test]
    public void ReportsEverySourceFileWhenSeveralMatchOneOtherFile()
    {
        string source = Path.Combine(_temp.Root, "source");
        string other = Path.Combine(_temp.Root, "other");
        string first = _temp.CreateFile(Path.Combine("source", "a1.txt"), "same");
        string second = _temp.CreateFile(Path.Combine("source", "a2.txt"), "same");
        _temp.CreateFile(Path.Combine("other", "b.txt"), "same");

        IReadOnlyList<CrossFolderDuplicate> result = CrossFolderDuplicateFinder.Find(
            source,
            other,
            _ => true,
            File.ReadAllBytes,
            TextWriter.Null,
            _ => { },
            CancellationToken.None);

        Assert.That(result.Select(item => item.Source.Path), Is.EquivalentTo(new[] { first, second }));
    }

    [Test]
    public void DoesNotReportSourceFilesMissingElsewhere()
    {
        string source = Path.Combine(_temp.Root, "source");
        string other = Path.Combine(_temp.Root, "other");
        _temp.CreateFile(Path.Combine("source", "a.txt"), "source only");
        _temp.CreateFile(Path.Combine("other", "b.txt"), "other content");

        IReadOnlyList<CrossFolderDuplicate> result = CrossFolderDuplicateFinder.Find(
            source,
            other,
            _ => true,
            File.ReadAllBytes,
            TextWriter.Null,
            _ => { },
            CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
