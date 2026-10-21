namespace FileCompare.Tests;

[TestFixture]
public class InvalidSymbolicLinkFinderTests
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
    public void FindsBrokenFileAndDirectoryLinksWithoutFollowingValidLinks()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Symbolic link creation is tested on Linux.");
        }

        string brokenFile = Path.Combine(_temp.Root, "broken-file");
        File.CreateSymbolicLink(brokenFile, Path.Combine(_temp.Root, "missing-file.txt"));

        string brokenDirectory = Path.Combine(_temp.Root, "broken-directory");
        Directory.CreateSymbolicLink(brokenDirectory, Path.Combine(_temp.Root, "missing-directory"));

        string validTarget = Directory.CreateDirectory(Path.Combine(_temp.Root, "target")).FullName;
        string validLink = Path.Combine(_temp.Root, "valid-link");
        Directory.CreateSymbolicLink(validLink, validTarget);

        _temp.CreateFile("normal.txt", "content");

        IReadOnlyList<InvalidSymbolicLink> result = InvalidSymbolicLinkFinder.Find(
            _temp.Root,
            FileSearchFilter.FromText(0, null, null),
            CancellationToken.None);

        Assert.That(
            result.Select(link => link.Path),
            Is.EquivalentTo(new[] { brokenFile, brokenDirectory }));
    }

    [Test]
    public async Task TrashCanRestoreABrokenSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Symbolic link creation is tested on Linux.");
        }

        string link = Path.Combine(_temp.Root, "broken-link");
        string missingTarget = Path.Combine(_temp.Root, "missing.txt");
        File.CreateSymbolicLink(link, missingTarget);

        Trash trash = new(Path.Combine(_temp.Root, "trash"));
        await trash.DeleteSymbolicLinkAsync(link);

        Assert.That(File.Exists(link), Is.False);

        Trash restarted = new(trash.TrashFolder);
        TrashEntry entry = restarted.GetEntries().Single();
        await restarted.RestoreAsync(entry);

        Assert.Multiple(() =>
        {
            Assert.That(entry.IsSymbolicLink, Is.True);
            Assert.That(new FileInfo(link).LinkTarget, Is.EqualTo(missingTarget));
            Assert.That(File.Exists(missingTarget), Is.False);
        });
    }
}
