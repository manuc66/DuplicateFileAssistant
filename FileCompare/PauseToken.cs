namespace FileCompare;

/// <summary>
/// Cooperative pause gate for long-running file operations. Operations wait on
/// <see cref="WaitWhilePausedAsync"/> between items, so a pause always takes effect at a
/// safe boundary: the file currently being moved is completed before the operation stops.
/// </summary>
public sealed class PauseToken
{
    private readonly object _gate = new();
    private TaskCompletionSource _resume = CreateCompleted();
    private bool _isPaused;

    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _isPaused;
            }
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_isPaused)
            {
                return;
            }

            _isPaused = true;
            _resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        TaskCompletionSource resume;

        lock (_gate)
        {
            if (!_isPaused)
            {
                return;
            }

            _isPaused = false;
            resume = _resume;
        }

        resume.TrySetResult();
    }

    public async Task WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task wait;

            lock (_gate)
            {
                if (!_isPaused)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return;
                }

                wait = _resume.Task;
            }

            await wait.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static TaskCompletionSource CreateCompleted()
    {
        TaskCompletionSource source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
