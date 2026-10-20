using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class ViewModelBase : ReactiveObject
{
    private readonly object _progressLock = new();
    private CancellationTokenSource? _operationCancellation;
    private int _pendingProgress;
    private bool _progressUpdateScheduled;
    private int _progressValue;
    private bool _isBusy;
    private string _status = "Ready.";

    public int ProgressValue
    {
        get => _progressValue;
        protected set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        protected set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public string Status
    {
        get => _status;
        protected set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    protected async Task RunOperationAsync(Func<CancellationToken, Task> operation, string runningStatus)
    {
        if (IsBusy)
        {
            return;
        }

        OnUiThread(() =>
        {
            IsBusy = true;
            Status = runningStatus;
        });

        _operationCancellation = new CancellationTokenSource();

        try
        {
            await operation(_operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            OnUiThread(() => Status = "Stopped.");
        }
        catch (Exception ex)
        {
            OnUiThread(() => Status = ex.Message);
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            OnUiThread(() => IsBusy = false);
        }
    }

    protected void StopOperation()
    {
        _operationCancellation?.Cancel();
    }

    protected void UpdateProgress(int progress)
    {
        lock (_progressLock)
        {
            _pendingProgress = progress;

            if (_progressUpdateScheduled)
            {
                return;
            }

            _progressUpdateScheduled = true;
        }

        Dispatcher.UIThread.Post(() =>
        {
            int value;

            lock (_progressLock)
            {
                value = _pendingProgress;
                _progressUpdateScheduled = false;
            }

            ProgressValue = value;
        });
    }

    protected static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
