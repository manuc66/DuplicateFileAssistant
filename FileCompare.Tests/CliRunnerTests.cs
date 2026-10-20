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
