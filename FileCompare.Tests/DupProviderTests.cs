namespace FileCompare.Tests;

[TestFixture]
public class DupProviderTests
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
    public void FindDuplicateByHash_KeepsCaseDifferentPathsDistinct_OnLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Test requires a case-sensitive file system.");
        }

        _temp.CreateFile("a.txt", "same content");
        _temp.CreateFile("A.txt", "same content");

        Dictionary<string, HashSet<string>> result = DupProvider.FindDuplicateByHash(
            _temp.Root,
            SearchOption.TopDirectoryOnly,
            _ => true,
            TextWriter.Null,
            _ => { },
            File.ReadAllText,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.Values.Single(), Has.Count.EqualTo(2));
        });
    }
}
