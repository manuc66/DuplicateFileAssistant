namespace FileCompare.Tests;

[TestFixture]
public class FileComparisonTests
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
    public void FastFileCompare_DoesNotTreatCaseDifferingPathsAsSameFile_OnLinux()
    {
        RequireCaseSensitiveFileSystem();

        string lower = _temp.CreateFile("file.txt", "a");
        string upper = _temp.CreateFile("FILE.txt", "b");

        Assert.That(
            FastFileCompare.AreFilesEqual(new FileInfo(lower), new FileInfo(upper)),
            Is.False);
    }

    [Test]
    public void FileComparer_DoesNotTreatCaseDifferingPathsAsSameFile_OnLinux()
    {
        RequireCaseSensitiveFileSystem();

        string lower = _temp.CreateFile("file.txt", "a");
        string upper = _temp.CreateFile("FILE.txt", "b");

        Assert.That(new ReadWholeFileAtOnce(lower, upper).Compare(), Is.False);
    }

    [Test]
    public void EightByteComparer_DetectsDifferenceInTrailingBytes()
    {
        byte[] first = Enumerable.Repeat((byte)1, 9).ToArray();
        byte[] second = (byte[])first.Clone();
        second[8] = 2;

        string firstPath = _temp.CreateFile("first.bin", first);
        string secondPath = _temp.CreateFile("second.bin", second);

        Assert.That(
            new ReadWholeFileAtOnceCompareEightByteAtOnce(firstPath, secondPath).Compare(),
            Is.False);
    }

    [Test]
    public void NaiveFileComparer_DetectsDifferenceInTrailingBytes()
    {
        byte[] first = Enumerable.Repeat((byte)1, 9).ToArray();
        byte[] second = (byte[])first.Clone();
        second[8] = 2;

        string firstPath = _temp.CreateFile("first.bin", first);
        string secondPath = _temp.CreateFile("second.bin", second);

        Assert.That(new NaiveFileComparer(firstPath, secondPath).Compare(), Is.False);
    }

    [Test]
    public void ReadFileInChunksAndCompareAvx2_UsesPooledBuffers()
    {
        AssertUsesPooledBuffers(
            new ReadFileInChunksAndCompareAvx2(
                _temp.CreateFile("first.bin", new byte[1024 * 1024]),
                _temp.CreateFile("second.bin", new byte[1024 * 1024])));
    }

    [Test]
    public void ReadFileInChunksAndCompareVector_UsesPooledBuffers()
    {
        AssertUsesPooledBuffers(
            new ReadFileInChunksAndCompareVector(
                _temp.CreateFile("first.bin", new byte[1024 * 1024]),
                _temp.CreateFile("second.bin", new byte[1024 * 1024]),
                0));
    }

    [Test]
    public void FastFileCompare_DetectsDifferenceInTrailingBytes()
    {
        byte[] first = Enumerable.Repeat((byte)1, 9).ToArray();
        byte[] second = (byte[])first.Clone();
        second[8] = 2;

        string firstPath = _temp.CreateFile("first.bin", first);
        string secondPath = _temp.CreateFile("second.bin", second);

        Assert.That(
            FastFileCompare.AreFilesEqual(new FileInfo(firstPath), new FileInfo(secondPath)),
            Is.False);
    }

    private static void AssertUsesPooledBuffers(FileComparer comparer)
    {
        Assert.That(comparer.Compare(), Is.True);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(comparer.Compare(), Is.True);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(
            allocated,
            Is.LessThan(128 * 1024),
            $"The comparer allocated {allocated} bytes; large byte buffers should come from ArrayPool.");
    }

    private static void RequireCaseSensitiveFileSystem()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Test requires a case-sensitive file system.");
        }
    }
}
