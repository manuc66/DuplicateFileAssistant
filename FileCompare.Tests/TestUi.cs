using Avalonia.Threading;

namespace FileCompare.Tests;

internal static class TestUi
{
    /// <summary>
    /// Pumps the headless dispatcher until the condition holds. ReactiveCommand
    /// observers may run after the awaited command result, so a single RunJobs
    /// is not a reliable synchronisation point.
    /// </summary>
    public static async Task WaitForAsync(Func<bool> condition, string description,
        int timeoutMilliseconds = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

        while (true)
        {
            Dispatcher.UIThread.RunJobs();

            if (condition())
            {
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail($"Timed out waiting for {description}.");
            }

            await Task.Delay(10);
        }
    }
}
