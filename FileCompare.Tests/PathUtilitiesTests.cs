using FileCompare;

namespace FileCompare.Tests;

[TestFixture]
public class PathUtilitiesTests
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
    public void IsUnderAcceptsTheFolderAndItsDescendants()
    {
        string folder = Path.Combine(_temp.Root, "folder");
        string child = Path.Combine(folder, "sub", "file.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(child)!);

        Assert.Multiple(() =>
        {
            Assert.That(PathUtilities.IsUnder(folder, folder), Is.True);
            Assert.That(PathUtilities.IsUnder(child, folder), Is.True);
            Assert.That(PathUtilities.IsUnder(folder + Path.DirectorySeparatorChar, folder), Is.True);
            Assert.That(PathUtilities.IsUnder(folder, folder + Path.DirectorySeparatorChar), Is.True);
        });
    }

    [Test]
    public void IsUnderRejectsSiblingsSharingTheFolderPrefix()
    {
        string folder = Path.Combine(_temp.Root, "photos");
        string sibling = Path.Combine(_temp.Root, "photos-archive", "file.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(PathUtilities.IsUnder(sibling, folder), Is.False);
            Assert.That(PathUtilities.IsUnder(_temp.Root, folder), Is.False);
        });
    }

    [Test]
    public void IsUnderHandlesTheFilesystemRoot()
    {
        string root = Path.GetPathRoot(Path.GetFullPath(_temp.Root))!;
        string child = Path.Combine(_temp.Root, "file.txt");

        Assert.Multiple(() =>
        {
            Assert.That(PathUtilities.IsUnder(child, root), Is.True);
            Assert.That(PathUtilities.IsUnder(root, root), Is.True);
            Assert.That(PathUtilities.IsUnder(root, _temp.Root), Is.False);
        });
    }

    [Test]
    public void IsUnderResolvesRelativePaths()
    {
        string folder = Path.Combine(_temp.Root, "folder");
        Directory.CreateDirectory(folder);
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, Path.Combine(folder, "a.txt"));
        string relativeFolder = Path.GetRelativePath(Environment.CurrentDirectory, folder);

        Assert.That(PathUtilities.IsUnder(relative, relativeFolder), Is.True);
    }

    [Test]
    public void TryExpandPathRejectsInvalidInput()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PathUtilities.TryExpandPath(null, out _), Is.False);
            Assert.That(PathUtilities.TryExpandPath("   ", out _), Is.False);
            Assert.That(PathUtilities.TryExpandPath("invalid\0path", out _), Is.False);
            Assert.That(PathUtilities.TryExpandPath(_temp.Root, out string expanded), Is.True);
            Assert.That(expanded, Is.EqualTo(Path.GetFullPath(_temp.Root)));
        });
    }

    [Test]
    public void ExpandPathExpandsTheTildePrefix()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string expected = Path.Combine(home, "DuplicateFileAssistantTests");

        Assert.Multiple(() =>
        {
            Assert.That(PathUtilities.ExpandPath("~"), Is.EqualTo(Path.GetFullPath(home)));
            Assert.That(PathUtilities.ExpandPath("~/DuplicateFileAssistantTests"), Is.EqualTo(expected));
        });
    }
}
