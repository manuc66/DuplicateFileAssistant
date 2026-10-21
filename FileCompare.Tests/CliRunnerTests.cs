using System.Text.Json;
using DuplicateFileAssistant.Cli;

namespace FileCompare.Tests;

[TestFixture]
public class CliRunnerTests
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
    public async Task DuplicatesCommandOutputsJson()
    {
        _temp.CreateFile("a.txt", "same");
        _temp.CreateFile("b.txt", "same");
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "duplicates", "--path", _temp.Root, "--json" },
            output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement results = document.RootElement.GetProperty("results");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(document.RootElement.GetProperty("affected").GetInt32(), Is.EqualTo(1));
            Assert.That(results.GetArrayLength(), Is.EqualTo(1));
            Assert.That(results[0].GetProperty("files").GetArrayLength(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CacheCommandReportsAndClears()
    {
        string cacheDirectory = Path.Combine(_temp.Root, "cache");
        string modelsDirectory = Path.Combine(cacheDirectory, "models");
        Directory.CreateDirectory(cacheDirectory);

        string file = _temp.CreateFile("a.txt", "a");
        ScanCache scan = new(Path.Combine(cacheDirectory, "scan-cache.json"));
        scan.GetOrCompute("sha256", file, _ => new byte[] { 1, 2, 3 });
        scan.Save();

        StringWriter output = new();
        int exitCode = await CliRunner.RunAsync(
            new[]
            {
                "cache", "--json", "--cache-dir", cacheDirectory, "--model-dir", modelsDirectory
            },
            output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(document.RootElement.GetProperty("entries").GetArrayLength(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("totalBytes").GetInt64(), Is.GreaterThan(0));
        });

        output = new StringWriter();
        exitCode = await CliRunner.RunAsync(
            new[] { "cache", "--clear", "--apply", "--cache-dir", cacheDirectory },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(File.Exists(Path.Combine(cacheDirectory, "scan-cache.json")), Is.False);
        });
    }

    [Test]
    public async Task CacheClearRequiresApply()
    {
        string cacheDirectory = Path.Combine(_temp.Root, "cache");
        Directory.CreateDirectory(cacheDirectory);
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "cache", "--clear", "--cache-dir", cacheDirectory },
            output);

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public async Task CacheCommandRejectsAFileAsTheCacheDirectory()
    {
        string file = _temp.CreateFile("cache-file", "not a directory");
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "cache", "--cache-dir", file },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("is a file"));
        });
    }

    [Test]
    public async Task DuplicatesCommandUsesTheScanCache()
    {
        _temp.CreateFile("a.txt", "same");
        _temp.CreateFile("b.txt", "same");
        string cachePath = Path.Combine(_temp.Root, "hashes.json");
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "duplicates", "--path", _temp.Root, "--json", "--cache-path", cachePath },
            output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(File.Exists(cachePath), Is.True);
            Assert.That(new FileInfo(cachePath).Length, Is.GreaterThan(2));
            Assert.That(document.RootElement.GetProperty("affected").GetInt32(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EmptyFilesCommandIsDryRunByDefaultAndAppliesWithFlag()
    {
        string empty = _temp.CreateFile("empty.txt", string.Empty);
        string trashPath = Path.Combine(_temp.Root, "trash");
        StringWriter output = new();

        await CliRunner.RunAsync(new[] { "empty-files", "--path", _temp.Root, "--json" }, output);
        Assert.That(File.Exists(empty), Is.True);

        output = new StringWriter();
        await CliRunner.RunAsync(
            new[] { "empty-files", "--path", _temp.Root, "--apply", "--trash", trashPath, "--json" },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(empty), Is.False);
            Assert.That(new Trash(trashPath).GetEntries(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task CommandsExcludeTheTrashFolder()
    {
        string trashPath = Path.Combine(_temp.Root, "trash");
        Directory.CreateDirectory(trashPath);
        File.WriteAllText(Path.Combine(trashPath, "ghost.txt"), string.Empty);
        StringWriter output = new();

        await CliRunner.RunAsync(
            new[] { "empty-files", "--path", _temp.Root, "--trash", trashPath, "--json" },
            output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.That(document.RootElement.GetProperty("results").GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task UnknownCommandsAreRejected()
    {
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "does-not-exist", "--path", _temp.Root },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("Unknown command"));
        });
    }

    [Test]
    public async Task MissingPathIsRejected()
    {
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(new[] { "duplicates" }, output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("Missing required option"));
        });
    }

    [Test]
    public async Task ANonExistingPathIsRejected()
    {
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "duplicates", "--path", Path.Combine(_temp.Root, "missing") },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("Directory not found"));
        });
    }

    [Test]
    public async Task InvalidNumericOptionsAreRejected()
    {
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "big-files", "--path", _temp.Root, "--max", "not-a-number" },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("Invalid --max"));
        });
    }

    [Test]
    public async Task ANegativeMaximumIsRejected()
    {
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "big-files", "--path", _temp.Root, "--max", "-1" },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("Invalid --max"));
        });
    }

    [Test]
    public async Task AnUnknownHashAlgorithmIsRejected()
    {
        _temp.CreateFile("a.txt", "same");
        _temp.CreateFile("b.txt", "same");
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "duplicates", "--path", _temp.Root, "--algorithm", "crc32" },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("Unsupported algorithm"));
        });
    }

    [TestCase("--threshold", "65")]
    [TestCase("--semantic-threshold", "1.5")]
    [TestCase("--semantic-margin", "-0.1")]
    [TestCase("--min-size", "-1")]
    public async Task InvalidSimilarImageOptionsAreRejected(string option, string value)
    {
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "similar-images", "--path", _temp.Root, option, value },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain($"Invalid {option}"));
        });
    }

    [Test]
    public async Task InvalidSymlinksCommandIsDryRunByDefaultAndAppliesWithFlag()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Symbolic link creation is tested on Linux.");
        }

        string link = Path.Combine(_temp.Root, "broken-link");
        File.CreateSymbolicLink(link, Path.Combine(_temp.Root, "missing.txt"));
        string trashPath = Path.Combine(_temp.Root, "trash");
        StringWriter output = new();

        await CliRunner.RunAsync(
            new[] { "invalid-symlinks", "--path", _temp.Root, "--json", "--trash", trashPath },
            output);

        using JsonDocument dryRun = JsonDocument.Parse(output.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(link), Is.True);
            Assert.That(dryRun.RootElement.GetProperty("applied").GetBoolean(), Is.False);
            Assert.That(dryRun.RootElement.GetProperty("affected").GetInt32(), Is.EqualTo(1));
            Assert.That(dryRun.RootElement.GetProperty("results")[0].GetProperty("target").GetString(),
                Is.EqualTo(Path.Combine(_temp.Root, "missing.txt")));
        });

        output = new StringWriter();
        await CliRunner.RunAsync(
            new[] { "invalid-symlinks", "--path", _temp.Root, "--json", "--apply", "--trash", trashPath },
            output);

        using JsonDocument applied = JsonDocument.Parse(output.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(new FileInfo(link).LinkTarget, Is.Null);
            Assert.That(applied.RootElement.GetProperty("applied").GetBoolean(), Is.True);
            Assert.That(applied.RootElement.GetProperty("affected").GetInt32(), Is.EqualTo(1));
            Assert.That(new Trash(trashPath).GetEntries(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task InvalidSymlinksCommandRespectsExclusions()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Symbolic link creation is tested on Linux.");
        }

        string excludedFolder = Directory.CreateDirectory(Path.Combine(_temp.Root, "excluded")).FullName;
        File.CreateSymbolicLink(
            Path.Combine(excludedFolder, "broken-link"),
            Path.Combine(_temp.Root, "missing.txt"));
        StringWriter output = new();

        await CliRunner.RunAsync(
            new[] { "invalid-symlinks", "--path", _temp.Root, "--json", "--exclude", excludedFolder },
            output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());

        Assert.That(document.RootElement.GetProperty("results").GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task BigFilesCommandRespectsSizeAndExtensionFilters()
    {
        string largeBin = _temp.CreateFile("large.bin", new string('a', 100 * 1024));
        _temp.CreateFile("large.log", new string('b', 100 * 1024));
        _temp.CreateFile("small.bin", new string('c', 1024));
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[]
            {
                "big-files", "--path", _temp.Root, "--json",
                "--min-size", "5 KB", "--max-size", "200 KB", "--extensions", "bin"
            },
            output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement results = document.RootElement.GetProperty("results");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(results.GetArrayLength(), Is.EqualTo(1));
            Assert.That(results[0].GetProperty("path").GetString(), Is.EqualTo(largeBin));
        });
    }

    [Test]
    public async Task TemporaryCommandRespectsSizeAndExtensionFilters()
    {
        _temp.CreateFile("download.tmp", new string('a', 10 * 1024));
        _temp.CreateFile("small.tmp", new string('b', 1024));
        string backup = _temp.CreateFile("document.bak", new string('c', 10 * 1024));
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[]
            {
                "temporary", "--path", _temp.Root, "--json",
                "--min-size", "5 KB", "--extensions", "bak"
            },
            output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement results = document.RootElement.GetProperty("results");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(results.GetArrayLength(), Is.EqualTo(1));
            Assert.That(results[0].GetProperty("path").GetString(), Is.EqualTo(backup));
        });
    }

    [Test]
    public async Task BigFilesCommandRejectsAnInvalidMaximumSize()
    {
        StringWriter output = new();

        int exitCode = await CliRunner.RunAsync(
            new[] { "big-files", "--path", _temp.Root, "--max-size", "nonsense" },
            output);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(output.ToString(), Does.Contain("max-size"));
        });
    }

    [Test]
    public async Task BigFilesCommandRespectsTheMaximum()
    {
        _temp.CreateFile("small.txt", "1");
        string medium = _temp.CreateFile("medium.txt", "12345");
        string large = _temp.CreateFile("large.txt", "1234567890");
        StringWriter output = new();

        await CliRunner.RunAsync(
            new[] { "big-files", "--path", _temp.Root, "--max", "2", "--json" },
            output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement results = document.RootElement.GetProperty("results");

        Assert.Multiple(() =>
        {
            Assert.That(results.GetArrayLength(), Is.EqualTo(2));
            Assert.That(results[0].GetProperty("path").GetString(), Is.EqualTo(large));
            Assert.That(results[1].GetProperty("path").GetString(), Is.EqualTo(medium));
        });
    }

    [Test]
    public async Task EmptyFoldersCommandCanDeleteEmptyDirectories()
    {
        string empty = Directory.CreateDirectory(Path.Combine(_temp.Root, "empty")).FullName;
        StringWriter output = new();

        await CliRunner.RunAsync(
            new[] { "empty-folders", "--path", _temp.Root, "--apply", "--json" },
            output);

        Assert.That(Directory.Exists(empty), Is.False);
    }

    [Test]
    public async Task TemporaryCommandFindsKnownTemporaryFiles()
    {
        string temporary = _temp.CreateFile("document.tmp", "content");
        _temp.CreateFile("keep.txt", "content");
        StringWriter output = new();

        await CliRunner.RunAsync(
            new[] { "temporary", "--path", _temp.Root, "--json" },
            output);

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement results = document.RootElement.GetProperty("results");

        Assert.That(results.GetArrayLength(), Is.EqualTo(1));
        Assert.That(results[0].GetProperty("path").GetString(), Is.EqualTo(temporary));
    }
}
