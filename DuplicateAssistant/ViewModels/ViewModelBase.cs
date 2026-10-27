using System;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class ViewModelBase : ReactiveObject
{
    private readonly object _progressLock = new();
    private readonly BehaviorSubject<bool> _busySubject = new(false);
    private readonly PauseToken _pauseSignal = new();
    private CancellationTokenSource? _operationCancellation;
    private int _pendingProgress;
    private string _pendingProgressText = string.Empty;
    private bool _progressUpdateScheduled;
    private int _progressValue;
    private string _progressText = string.Empty;
    private bool _isProgressIndeterminate;
    private bool _isBusy;
    private bool _canPause;
    private bool _isPaused;
    private string _pauseButtonText = "Pause";
    private string _runningStatus = "Ready.";
    private string _status = "Ready.";

    protected ViewModelBase()
    {
        StopCommand = ReactiveCommand.Create(StopOperation);
        PauseResumeCommand = ReactiveCommand.Create(TogglePause);
    }

    public int ProgressValue
    {
        get => _progressValue;
        protected set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    /// <summary>
    /// Human-readable progress, for example "12/345 (3%) - photo.jpg" while files are
    /// moved, or "42%" while a scan reports a percentage.
    /// </summary>
    public string ProgressText
    {
        get => _progressText;
        private set => this.RaiseAndSetIfChanged(ref _progressText, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => this.RaiseAndSetIfChanged(ref _isProgressIndeterminate, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        protected set
        {
            this.RaiseAndSetIfChanged(ref _isBusy, value);
            _busySubject.OnNext(value);
        }
    }

    /// <summary>
    /// True while the current operation supports pausing between items. Scans keep this
    /// false because the finders cannot suspend themselves safely.
    /// </summary>
    public bool CanPause
    {
        get => _canPause;
        private set => this.RaiseAndSetIfChanged(ref _canPause, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set => this.RaiseAndSetIfChanged(ref _isPaused, value);
    }

    public string PauseButtonText
    {
        get => _pauseButtonText;
        private set => this.RaiseAndSetIfChanged(ref _pauseButtonText, value);
    }

    public string Status
    {
        get => _status;
        protected set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public ReactiveCommand<Unit, Unit> StopCommand { get; }

    public ReactiveCommand<Unit, Unit> PauseResumeCommand { get; }

    protected PauseToken PauseSignal => _pauseSignal;

    protected IObservable<bool> BusyObservable => _busySubject;

    protected async Task RunOperationAsync(Func<CancellationToken, Task> operation, string runningStatus,
        bool canPause = false)
    {
        if (IsBusy)
        {
            return;
        }

        _runningStatus = runningStatus;
        _pauseSignal.Resume();

        OnUiThread(() =>
        {
            IsBusy = true;
            Status = runningStatus;
            CanPause = canPause;
            IsPaused = false;
            PauseButtonText = "Pause";
            IsProgressIndeterminate = true;
            ProgressValue = 0;
            ProgressText = string.Empty;
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
            _pauseSignal.Resume();
            OnUiThread(() =>
            {
                IsBusy = false;
                CanPause = false;
                IsPaused = false;
                PauseButtonText = "Pause";
            });
        }
    }

    protected virtual void StopOperation()
    {
        _pauseSignal.Resume();
        _operationCancellation?.Cancel();
    }

    protected void UpdateProgress(int progress)
    {
        PostProgress(progress, $"{progress}%");
    }

    /// <summary>
    /// Reports item-based progress: <paramref name="completed"/> items out of
    /// <paramref name="total"/>, optionally with the file currently being handled.
    /// </summary>
    protected void ReportProgress(int completed, int total, string? item = null)
    {
        int safeTotal = Math.Max(total, 0);
        completed = Math.Clamp(completed, 0, safeTotal);
        int percent = safeTotal == 0 ? 0 : (int)Math.Round(completed * 100.0 / safeTotal);
        string text = $"{completed}/{safeTotal} ({percent}%)";

        if (!string.IsNullOrEmpty(item))
        {
            text = $"{text} - {Path.GetFileName(item)}";
        }

        PostProgress(percent, text);
    }

    private void PostProgress(int value, string text)
    {
        lock (_progressLock)
        {
            _pendingProgress = value;
            _pendingProgressText = text;

            if (_progressUpdateScheduled)
            {
                return;
            }

            _progressUpdateScheduled = true;
        }

        Dispatcher.UIThread.Post(() =>
        {
            int pendingValue;
            string pendingText;

            lock (_progressLock)
            {
                pendingValue = _pendingProgress;
                pendingText = _pendingProgressText;
                _progressUpdateScheduled = false;
            }

            ProgressValue = pendingValue;
            ProgressText = pendingText;
            IsProgressIndeterminate = false;
        });
    }

    private void TogglePause()
    {
        if (!CanPause || !IsBusy)
        {
            return;
        }

        if (_pauseSignal.IsPaused)
        {
            _pauseSignal.Resume();
            OnUiThread(() =>
            {
                IsPaused = false;
                PauseButtonText = "Pause";
                Status = _runningStatus;
            });
        }
        else
        {
            _pauseSignal.Pause();
            OnUiThread(() =>
            {
                IsPaused = true;
                PauseButtonText = "Resume";
                Status = string.IsNullOrEmpty(ProgressText) ? "Paused." : $"Paused. {ProgressText}";
            });
        }
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
