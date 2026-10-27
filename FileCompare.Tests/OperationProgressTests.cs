using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using DuplicateAssistant.Controls;
using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

[TestFixture]
public class OperationProgressTests
{
    [AvaloniaTest]
    public void ReportProgressPublishesPercentageAndCurrentFile()
    {
        ProbeViewModel viewModel = new();

        viewModel.Report(1, 4, Path.Combine("photos", "a.jpg"));
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ProgressValue, Is.EqualTo(25));
            Assert.That(viewModel.ProgressText, Does.Contain("1/4"));
            Assert.That(viewModel.ProgressText, Does.Contain("25%"));
            Assert.That(viewModel.ProgressText, Does.Contain("a.jpg"));
            Assert.That(viewModel.IsProgressIndeterminate, Is.False);
        });
    }

    [AvaloniaTest]
    public void OperationBarBindsToTheViewModel()
    {
        ProbeViewModel viewModel = new();
        OperationBar bar = new() { DataContext = viewModel };

        viewModel.Report(1, 4, Path.Combine("photos", "a.jpg"));
        Dispatcher.UIThread.RunJobs();

        ProgressBar progress = bar.GetLogicalDescendants().OfType<ProgressBar>().Single();
        TextBlock text = bar.GetLogicalDescendants().OfType<TextBlock>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(progress.Value, Is.EqualTo(25));
            Assert.That(text.Text, Does.Contain("1/4"));
            Assert.That(text.Text, Does.Contain("a.jpg"));
        });
    }

    [AvaloniaTest]
    public void OperationBarKeepsAStableBarWidthWhenTheTextChanges()
    {
        ProbeViewModel viewModel = new();
        OperationBar bar = new() { DataContext = viewModel };
        Window window = new() { Content = bar, Width = 800, Height = 100 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        bar.Measure(new Size(800, 40));
        bar.Arrange(new Rect(0, 0, 800, 40));
        ProgressBar progress = bar.GetLogicalDescendants().OfType<ProgressBar>().Single();
        double initialWidth = progress.Bounds.Width;
        Assert.That(initialWidth, Is.GreaterThan(0));

        viewModel.Report(1, 100_000, Path.Combine(new string('x', 120), "a-very-long-file-name.jpg"));
        Dispatcher.UIThread.RunJobs();
        bar.Measure(new Size(800, 40));
        bar.Arrange(new Rect(0, 0, 800, 40));

        Assert.That(progress.Bounds.Width, Is.EqualTo(initialWidth));
    }

    [AvaloniaTest]
    public async Task PauseBlocksTheOperationUntilResume()
    {
        ProbeViewModel viewModel = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource allowProgress = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task operation = viewModel.RunAsync(async cancellationToken =>
        {
            started.TrySetResult();
            await allowProgress.Task;
            await viewModel.WaitPausedAsync(cancellationToken);
            finished.TrySetResult();
        }, canPause: true);

        await TestUi.WaitForAsync(() => started.Task.IsCompleted, "the operation to start");
        await viewModel.PauseResumeCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsPaused, Is.True);
            Assert.That(viewModel.PauseButtonText, Is.EqualTo("Resume"));
        });

        allowProgress.TrySetResult();
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        Assert.That(finished.Task.IsCompleted, Is.False, "The operation must stay paused.");

        await viewModel.PauseResumeCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.IsPaused, Is.False);
        await TestUi.WaitForAsync(() => finished.Task.IsCompleted, "the operation to resume");
        await operation;
    }

    [AvaloniaTest]
    public async Task StopCancelsAPausedOperation()
    {
        ProbeViewModel viewModel = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource allowProgress = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task operation = viewModel.RunAsync(async cancellationToken =>
        {
            started.TrySetResult();
            await allowProgress.Task;
            await viewModel.WaitPausedAsync(cancellationToken);
        }, canPause: true);

        await TestUi.WaitForAsync(() => started.Task.IsCompleted, "the operation to start");
        await viewModel.PauseResumeCommand.Execute(Unit.Default).FirstAsync();
        Dispatcher.UIThread.RunJobs();
        allowProgress.TrySetResult();
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        await viewModel.StopCommand.Execute(Unit.Default).FirstAsync();
        await TestUi.WaitForAsync(() => !viewModel.IsBusy, "the operation to stop");
        await operation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsPaused, Is.False);
            Assert.That(viewModel.CanPause, Is.False);
            Assert.That(viewModel.Status, Is.EqualTo("Stopped."));
        });
    }

    private sealed class ProbeViewModel : ViewModelBase
    {
        public Task RunAsync(Func<CancellationToken, Task> operation, bool canPause)
        {
            return RunOperationAsync(operation, "Working...", canPause);
        }

        public Task WaitPausedAsync(CancellationToken cancellationToken)
        {
            return PauseSignal.WaitWhilePausedAsync(cancellationToken);
        }

        public void Report(int completed, int total, string? item = null)
        {
            ReportProgress(completed, total, item);
        }
    }
}
