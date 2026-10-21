using DuplicateAssistant.Business.FileHashing;

namespace FileCompare.Tests;

[TestFixture]
public class HashCancellationTests
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
    public void Blake3_ThrowsWhenCancellationIsRequested()
    {
        string file = _temp.CreateFile("file.bin", new byte[1024 * 1024]);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.That(
            () => new Blake3FileHashing().Hash(file, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Md5_ThrowsWhenCancellationIsRequested()
    {
        string file = _temp.CreateFile("file.bin", new byte[1024 * 1024]);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.That(
            () => new Md5FileHashing().Hash(file, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void XXHash_ThrowsWhenCancellationIsRequested()
    {
        string file = _temp.CreateFile("file.bin", new byte[1024 * 1024]);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.That(
            () => new XXHashFileHashing().Hash(file, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
