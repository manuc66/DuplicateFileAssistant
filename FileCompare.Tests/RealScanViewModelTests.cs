using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.Business;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

/// <summary>
/// Exercises the concrete scan view models, whose search functions are otherwise
/// only reached through the desktop application.
/// </summary>
[TestFixture]
public class RealScanViewModelTests
{
    private TempDirectory _temp = null!;
    private Trash _trash = null!;
    private FakeDeleteConfirmation _confirmation = null!;
    private FakeFolderPicker _folderPicker = null!;
    private FileManagerHandler _fileManager = null!;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempDirectory();
        _trash = new Trash(Path.Combine(_temp.Root, "trash"));
        _confirmation = new FakeDeleteConfirmation();
        _folderPicker = new FakeFolderPicker();
        _fileManager = new FileManagerHandler();
    }

    [TearDown]
    public void TearDown()
    {
        _temp.Dispose();
    }

    [AvaloniaTest]
    public async Task DuplicateContentFindsOnlyIdenticalFiles()
    {
        string first = _temp.CreateFile(Path.Combine("a", "first.txt"), "same content");
        string second = _temp.CreateFile(Path.Combine("b", "second.txt"), "same content");
        _temp.CreateFile(Path.Combine("c", "other.txt"), "other content");

        DuplicateContentInFolderViewModel viewModel = new(
            _trash,
            _temp.Root,
            _fileManager,
            _confirmation,
            _folderPicker);

        await viewModel.SearchCommand.Execute(Unit.Default).LastAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateCaseItems, Has.Count.EqualTo(1));
            Assert.That(
                viewModel.DuplicateCaseItems[0].Files.Select(file => file.FilePath),
                Is.EquivalentTo(new[] { first, second }));
            Assert.That(viewModel.Finished, Is.True);
        });
    }

    [AvaloniaTest]
    public async Task DuplicateNameGroupsFilesWithTheSameName()
    {
        string first = _temp.CreateFile(Path.Combine("a", "photo.jpg"), "one");
        string second = _temp.CreateFile(Path.Combine("b", "photo.jpg"), "two");
        _temp.CreateFile(Path.Combine("c", "other.jpg"), "three");

        DuplicateNameInFolderViewModel viewModel = new(
            _trash,
            _temp.Root,
            _fileManager,
            _confirmation,
            _folderPicker);

        await viewModel.SearchCommand.Execute(Unit.Default).LastAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateCaseItems, Has.Count.EqualTo(1));
            Assert.That(
                viewModel.DuplicateCaseItems[0].Files.Select(file => file.FilePath),
                Is.EquivalentTo(new[] { first, second }));
        });
    }

    [AvaloniaTest]
    public async Task DuplicateSizeGroupsFilesWithTheSameLength()
    {
        string first = _temp.CreateFile(Path.Combine("a", "first.bin"), "1234");
        string second = _temp.CreateFile(Path.Combine("b", "second.bin"), "abcd");
        _temp.CreateFile(Path.Combine("c", "other.bin"), "12345");

        DuplicateSizeInFolderViewModel viewModel = new(
            _trash,
            _temp.Root,
            _fileManager,
            _confirmation,
            _folderPicker);

        await viewModel.SearchCommand.Execute(Unit.Default).LastAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowHashOptions, Is.False);
            Assert.That(viewModel.DuplicateCaseItems, Has.Count.EqualTo(1));
            Assert.That(
                viewModel.DuplicateCaseItems[0].Files.Select(file => file.FilePath),
                Is.EquivalentTo(new[] { first, second }));
        });
    }

    [AvaloniaTest]
    public async Task TheExtensionSelectionFiltersTheScan()
    {
        string jpgFirst = _temp.CreateFile(Path.Combine("a", "photo.jpg"), "same");
        string jpgSecond = _temp.CreateFile(Path.Combine("b", "photo.jpg"), "same");
        _temp.CreateFile(Path.Combine("a", "photo.png"), "same");
        _temp.CreateFile(Path.Combine("b", "photo.png"), "same");

        DuplicateContentInFolderViewModel viewModel = new(
            _trash,
            _temp.Root,
            _fileManager,
            _confirmation,
            _folderPicker);
        viewModel.ExtensionSelection.AddExtension("jpg");

        await viewModel.SearchCommand.Execute(Unit.Default).LastAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(
            viewModel.DuplicateCaseItems[0].Files.Select(file => file.FilePath),
            Is.EquivalentTo(new[] { jpgFirst, jpgSecond }));
    }

    [AvaloniaTest]
    public async Task TheMinimumFileSizeFiltersTheScan()
    {
        _temp.CreateFile(Path.Combine("a", "small-1.txt"), "x");
        _temp.CreateFile(Path.Combine("b", "small-2.txt"), "x");
        string largeFirst = _temp.CreateFile(Path.Combine("a", "large-1.txt"), "0123456789");
        string largeSecond = _temp.CreateFile(Path.Combine("b", "large-2.txt"), "0123456789");

        DuplicateContentInFolderViewModel viewModel = new(
            _trash,
            _temp.Root,
            _fileManager,
            _confirmation,
            _folderPicker)
        {
            MinimumFileSizeText = "5"
        };

        await viewModel.SearchCommand.Execute(Unit.Default).LastAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(
            viewModel.DuplicateCaseItems[0].Files.Select(file => file.FilePath),
            Is.EquivalentTo(new[] { largeFirst, largeSecond }));
    }

    [AvaloniaTest]
    public async Task AnInvalidMinimumSizeIsReportedInsteadOfCrashing()
    {
        _temp.CreateFile(Path.Combine("a", "first.txt"), "same");
        _temp.CreateFile(Path.Combine("b", "second.txt"), "same");

        DuplicateContentInFolderViewModel viewModel = new(
            _trash,
            _temp.Root,
            _fileManager,
            _confirmation,
            _folderPicker)
        {
            MinimumFileSizeText = "not-a-size"
        };

        await viewModel.SearchCommand.Execute(Unit.Default).LastAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateCaseItems, Is.Empty);
            Assert.That(viewModel.SearchLog, Does.Contain("Minimum file size"));
            Assert.That(viewModel.Finished, Is.True);
        });
    }

    [AvaloniaTest]
    public async Task ExcludedFoldersAreNotScanned()
    {
        string includedFirst = _temp.CreateFile(Path.Combine("included", "copy.txt"), "same");
        string includedSecond = _temp.CreateFile(Path.Combine("included", "original.txt"), "same");
        _temp.CreateFile(Path.Combine("excluded", "copy.txt"), "same");
        string excludedFolder = Path.Combine(_temp.Root, "excluded");

        DuplicateContentInFolderViewModel viewModel = new(
            _trash,
            _temp.Root,
            _fileManager,
            _confirmation,
            _folderPicker)
        {
            ExcludedFolders = excludedFolder
        };

        await viewModel.SearchCommand.Execute(Unit.Default).LastAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(
            viewModel.DuplicateCaseItems.Single().Files.Select(file => file.FilePath),
            Is.EquivalentTo(new[] { includedFirst, includedSecond }));
    }
}
