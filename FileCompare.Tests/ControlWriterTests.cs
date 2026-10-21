using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using DuplicateAssistant.Business;

namespace FileCompare.Tests;

[TestFixture]
public class ControlWriterTests
{
    [AvaloniaTest]
    public async Task RapidWritesAreThrottledBeforeReachingTheUi()
    {
        FakeSearchLog log = new();

        using (ControlWriter writer = new(log))
        {
            Task writerTask = Task.Run(() =>
            {
                for (int index = 0; index < 50; index++)
                {
                    writer.WriteLine($"line {index}");
                    Thread.Sleep(10);
                }
            });

            while (!writerTask.IsCompleted)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            await writerTask;
        }

        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(log.Updates, Is.LessThan(10), "UI updates should be throttled.");
            Assert.That(log.SearchLog, Does.Contain("line 49"));
        });
    }
}
