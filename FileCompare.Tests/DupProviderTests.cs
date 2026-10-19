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

    [Test]
    public void FindDuplicateByHash_DoesNotTrustHashCollisions()
    {
        string first = _temp.CreateFile("a.txt", "aaaa");
        string second = _temp.CreateFile("b.txt", "bbbb");

        Dictionary<string, HashSet<string>> result = DupProvider.FindDuplicateByHash(
            _temp.Root,
            SearchOption.TopDirectoryOnly,
            _ => true,
            TextWriter.Null,
            _ => { },
            _ => "forced-collision",
            CancellationToken.None);

        Assert.That(result, Is.Empty, $"Files must be byte-identical: {first}, {second}");
    }

    [Test]
    public void FindDuplicateByHash_KeepsOnlyByteIdenticalFilesInACollisionGroup()
    {
        string first = _temp.CreateFile("a.txt", "same");
        string second = _temp.CreateFile("b.txt", "same");
        _temp.CreateFile("c.txt", "diff");

        Dictionary<string, HashSet<string>> result = DupProvider.FindDuplicateByHash(
            _temp.Root,
            SearchOption.TopDirectoryOnly,
            _ => true,
            TextWriter.Null,
            _ => { },
            _ => "forced-collision",
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.Values.Single(), Is.EquivalentTo(new[] { first, second }));
        });
    }

    [Test]
    public void FindDuplicateByHash_PropagatesCancellation()
    {
        _temp.CreateFile("a.txt", "same");
        _temp.CreateFile("b.txt", "same");
        using CancellationTokenSource cancellation = new();

        Assert.That(
            () => DupProvider.FindDuplicateByHash(
                _temp.Root,
                SearchOption.TopDirectoryOnly,
                _ => true,
                TextWriter.Null,
                _ => { },
                _ =>
                {
                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellation.Token);
                },
                cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void FindDuplicateByHash_SkipsFullHashWhenPartialHashesDiffer()
    {
        byte[] first = new byte[2 * 1024 * 1024];
        byte[] second = new byte[first.Length];
        first[0] = 1;
        Array.Fill(first, (byte)7, 1, first.Length - 1);
        second[0] = 2;
        Array.Fill(second, (byte)7, 1, second.Length - 1);

        _temp.CreateFile("first.bin", first);
        _temp.CreateFile("second.bin", second);
        int fullHashCalls = 0;

        Dictionary<string, HashSet<string>> result = DupProvider.FindDuplicateByHash(
            _temp.Root,
            SearchOption.TopDirectoryOnly,
            _ => true,
            TextWriter.Null,
            _ => { },
            _ =>
            {
                fullHashCalls++;
                return "same";
            },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(fullHashCalls, Is.Zero);
            Assert.That(result, Is.Empty);
        });
    }

    [Test]
    public void FindDuplicateByHash_StillHashesIdenticalLargeFiles()
    {
        byte[] content = new byte[2 * 1024 * 1024];
        Array.Fill(content, (byte)7);

        _temp.CreateFile("first.bin", content);
        _temp.CreateFile("second.bin", content);
        int fullHashCalls = 0;

        Dictionary<string, HashSet<string>> result = DupProvider.FindDuplicateByHash(
            _temp.Root,
            SearchOption.TopDirectoryOnly,
            _ => true,
            TextWriter.Null,
            _ => { },
            _ =>
            {
                Interlocked.Increment(ref fullHashCalls);
                return "same";
            },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(fullHashCalls, Is.EqualTo(2));
            Assert.That(result, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void FindDuplicateByHash_HashesCandidatesInParallel()
    {
        if (Environment.ProcessorCount < 2)
        {
            Assert.Ignore("Parallel hashing requires at least two logical processors.");
        }

        _temp.CreateFile("a.txt", "same");
        _temp.CreateFile("b.txt", "same");

        using Barrier barrier = new(2);
        int calls = 0;

        string Hash(string path)
        {
            if (Interlocked.Increment(ref calls) <= 2)
            {
                Assert.That(
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5)),
                    Is.True,
                    "Hashing must run in parallel.");
            }

            return File.ReadAllText(path);
        }

        Dictionary<string, HashSet<string>> result = DupProvider.FindDuplicateByHash(
            _temp.Root,
            SearchOption.TopDirectoryOnly,
            _ => true,
            TextWriter.Null,
            _ => { },
            Hash,
            CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public void FindDuplicateByHash_TwoFolders_DoesNotTrustHashCollisions()
    {
        string firstFolder = Path.Combine(_temp.Root, "first");
        string secondFolder = Path.Combine(_temp.Root, "second");
        _temp.CreateFile(Path.Combine("first", "a.txt"), "aaaa");
        _temp.CreateFile(Path.Combine("second", "b.txt"), "bbbb");

        Dictionary<string, (HashSet<string> inA, HashSet<string> inB)> result =
            DupProvider.FindDuplicateByHash(
                firstFolder,
                SearchOption.TopDirectoryOnly,
                secondFolder,
                SearchOption.TopDirectoryOnly,
                _ => true,
                TextWriter.Null,
                _ => "forced-collision",
                CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
