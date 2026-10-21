namespace FileCompare.Tests;

[TestFixture]
public class EmptyDirectoryServiceTests
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
    public void FindsEmptyDirectoriesDeepestFirstAndIncludesParentsThatBecomeEmpty()
    {
        string root = _temp.Root;
        string empty = Directory.CreateDirectory(Path.Combine(root, "empty")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;
        string nestedLeaf = Directory.CreateDirectory(Path.Combine(root, "nested", "leaf")).FullName;
        string parent = Directory.CreateDirectory(Path.Combine(root, "parent")).FullName;
        string onlyEmptyChild = Directory.CreateDirectory(Path.Combine(parent, "only-empty-child")).FullName;
        string occupied = Directory.CreateDirectory(Path.Combine(root, "occupied")).FullName;
        string occupiedEmptyChild = Directory.CreateDirectory(Path.Combine(occupied, "empty-child")).FullName;
        _temp.CreateFile(Path.Combine("occupied", "file.txt"), "content");

        IReadOnlyList<string> result = EmptyDirectoryService.FindEmptyDirectories(root, CancellationToken.None);
        List<string> ordered = result.ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EquivalentTo(new[] { empty, nestedLeaf, nested, onlyEmptyChild, parent, occupiedEmptyChild }));
            Assert.That(ordered.IndexOf(nestedLeaf), Is.LessThan(ordered.IndexOf(nested)));
            Assert.That(result, Does.Not.Contain(occupied));
            Assert.That(result, Does.Not.Contain(root));
        });
    }

    [Test]
    public void DeletesOnlyTheReportedDirectoriesAndKeepsTheRoot()
    {
        string root = _temp.Root;
        string empty = Directory.CreateDirectory(Path.Combine(root, "empty")).FullName;
        string leaf = Directory.CreateDirectory(Path.Combine(root, "nested", "leaf")).FullName;
        string occupied = Directory.CreateDirectory(Path.Combine(root, "occupied")).FullName;
        _temp.CreateFile(Path.Combine("occupied", "file.txt"), "content");

        IReadOnlyList<string> directories = EmptyDirectoryService.FindEmptyDirectories(root, CancellationToken.None);
        int deleted = EmptyDirectoryService.DeleteEmptyDirectories(directories, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.EqualTo(directories.Count));
            Assert.That(Directory.Exists(empty), Is.False);
            Assert.That(Directory.Exists(leaf), Is.False);
            Assert.That(Directory.Exists(Path.Combine(root, "nested")), Is.False);
            Assert.That(Directory.Exists(occupied), Is.True);
            Assert.That(Directory.Exists(root), Is.True);
        });
    }

    [Test]
    public void DoesNotFollowOrDeleteSymbolicLinks()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Symbolic link creation is tested on Linux.");
        }

        using TempDirectory targetDirectory = new();
        string link = Path.Combine(_temp.Root, "link");
        Directory.CreateSymbolicLink(link, targetDirectory.Root);

        IReadOnlyList<string> result = EmptyDirectoryService.FindEmptyDirectories(_temp.Root, CancellationToken.None);

        Assert.That(result, Does.Not.Contain(link));
    }
}
