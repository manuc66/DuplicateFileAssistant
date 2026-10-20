namespace FileCompare.Tests;

[TestFixture]
public class MergePlannerTests
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
    public void PlansMovesDuplicatesAndConflicts()
    {
        string source = Path.Combine(_temp.Root, "source");
        string destination = Path.Combine(_temp.Root, "destination");
        string moveSource = _temp.CreateFile(Path.Combine("source", "move.txt"), "move");
        string duplicateSource = _temp.CreateFile(Path.Combine("source", "duplicate.txt"), "duplicate");
        string conflictSource = _temp.CreateFile(Path.Combine("source", "conflict.txt"), "source content");
        _temp.CreateFile(Path.Combine("destination", "duplicate.txt"), "duplicate");
        _temp.CreateFile(Path.Combine("destination", "conflict.txt"), "destination content");

        IReadOnlyList<MergePlanItem> plan = MergePlanner.CreatePlan(source, destination, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(plan, Has.Count.EqualTo(3));
            Assert.That(plan.Single(item => item.SourcePath == moveSource).Action, Is.EqualTo(MergeAction.Move));
            Assert.That(plan.Single(item => item.SourcePath == duplicateSource).Action, Is.EqualTo(MergeAction.TrashDuplicate));
            Assert.That(plan.Single(item => item.SourcePath == conflictSource).Action, Is.EqualTo(MergeAction.Conflict));
        });
    }

    [Test]
    public void PreservesRelativePaths()
    {
        string source = Path.Combine(_temp.Root, "source");
        string destination = Path.Combine(_temp.Root, "destination");
        _temp.CreateFile(Path.Combine("source", "nested", "image.jpg"), "content");

        MergePlanItem item = MergePlanner.CreatePlan(source, destination, CancellationToken.None).Single();

        Assert.That(item.DestinationPath, Is.EqualTo(Path.Combine(destination, "nested", "image.jpg")));
    }

    [Test]
    public void RejectsNestedSourceAndDestination()
    {
        string source = Path.Combine(_temp.Root, "source");
        string destination = Path.Combine(source, "destination");
        Directory.CreateDirectory(destination);

        Assert.That(
            () => MergePlanner.CreatePlan(source, destination, CancellationToken.None),
            Throws.InvalidOperationException);
    }

    [Test]
    public async Task ExecutesThePlanWithoutOverwritingConflicts()
    {
        string source = Path.Combine(_temp.Root, "source");
        string destination = Path.Combine(_temp.Root, "destination");
        string moveSource = _temp.CreateFile(Path.Combine("source", "move.txt"), "move");
        string duplicateSource = _temp.CreateFile(Path.Combine("source", "duplicate.txt"), "duplicate");
        string conflictSource = _temp.CreateFile(Path.Combine("source", "conflict.txt"), "source content");
        _temp.CreateFile(Path.Combine("destination", "duplicate.txt"), "duplicate");
        string conflictDestination = _temp.CreateFile(Path.Combine("destination", "conflict.txt"), "destination content");

        Trash trash = new(Path.Combine(_temp.Root, "trash"));
        IReadOnlyList<MergePlanItem> plan = MergePlanner.CreatePlan(source, destination, CancellationToken.None);

        MergeResult result = await MergeExecutor.ExecuteAsync(plan, trash, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Moved, Is.EqualTo(1));
            Assert.That(result.TrashedDuplicates, Is.EqualTo(1));
            Assert.That(result.Conflicts, Is.EqualTo(1));
            Assert.That(File.Exists(moveSource), Is.False);
            Assert.That(File.Exists(Path.Combine(destination, "move.txt")), Is.True);
            Assert.That(File.Exists(duplicateSource), Is.False);
            Assert.That(File.Exists(conflictSource), Is.True);
            Assert.That(File.ReadAllText(conflictDestination), Is.EqualTo("destination content"));
            Assert.That(Directory.GetFiles(trash.TrashFolder), Has.Length.EqualTo(1));
        });
    }
}
