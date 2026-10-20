using System;
using System.IO;
using System.Text;
using System.Threading;
using Avalonia.Threading;
using DuplicateAssistant.ViewModels;

namespace DuplicateAssistant.Business;

internal class ControlWriter : TextWriter
{
    private const int MaxLogLength = 100_000;
    private const int FlushIntervalMilliseconds = 100;

    private readonly object _sync = new();
    private readonly BoundedTextBuffer _content = new(MaxLogLength);
    private readonly IHaveSearchLog _searchLogHolder;
    private readonly Timer _flushTimer;
    private int _updateScheduled;
    private bool _disposed;

    public ControlWriter(IHaveSearchLog searchLogHolder)
    {
        _searchLogHolder = searchLogHolder;
        _flushTimer = new Timer(
            _ => Dispatcher.UIThread.Post(FlushToUi),
            null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    public override void Write(char value)
    {
        Append(value);
    }

    public override void Write(string? value)
    {
        Append(value);
    }

    public override void WriteLine()
    {
        AppendLine(null);
    }

    public override void WriteLine(string? value)
    {
        AppendLine(value);
    }

    public override void WriteLine(object? value)
    {
        AppendLine(value?.ToString());
    }

    public override Encoding Encoding => Encoding.UTF8;

    private void Append(char value)
    {
        bool scheduleUpdate;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _content.Append(value);
            scheduleUpdate = Interlocked.Exchange(ref _updateScheduled, 1) == 0;
        }

        if (scheduleUpdate)
        {
            _flushTimer.Change(FlushIntervalMilliseconds, Timeout.Infinite);
        }
    }

    private void Append(string? value)
    {
        bool scheduleUpdate;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _content.Append(value);
            scheduleUpdate = Interlocked.Exchange(ref _updateScheduled, 1) == 0;
        }

        if (scheduleUpdate)
        {
            _flushTimer.Change(FlushIntervalMilliseconds, Timeout.Infinite);
        }
    }

    private void AppendLine(string? value)
    {
        bool scheduleUpdate;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _content.AppendLine(value);
            scheduleUpdate = Interlocked.Exchange(ref _updateScheduled, 1) == 0;
        }

        if (scheduleUpdate)
        {
            _flushTimer.Change(FlushIntervalMilliseconds, Timeout.Infinite);
        }
    }

    private void FlushToUi()
    {
        string content;

        lock (_sync)
        {
            if (_disposed)
            {
                Volatile.Write(ref _updateScheduled, 0);
                return;
            }

            Volatile.Write(ref _updateScheduled, 0);
            content = _content.ToString();
        }

        _searchLogHolder.SearchLog = content;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                FlushToUi();
            }
            else
            {
                Dispatcher.UIThread.Invoke(FlushToUi);
            }

            lock (_sync)
            {
                _disposed = true;
            }

            _flushTimer.Dispose();
            _content.Dispose();
        }

        base.Dispose(disposing);
    }
}
