using System;
using System.IO;
using System.Text;
using System.Threading;
using Avalonia.Threading;
using DuplicateAssistant.ViewModels;

namespace DuplicateAssistant.Business;

internal class ControlWriter : TextWriter
{
    private readonly object _sync = new();
    private readonly StringBuilder _content = new();
    private readonly IHaveSearchLog _searchLogHolder;
    private int _updateScheduled;

    public ControlWriter(IHaveSearchLog searchLogHolder)
    {
        _searchLogHolder = searchLogHolder;
    }

    public override void Write(char value)
    {
        Append(value.ToString());
    }

    public override void Write(string? value)
    {
        Append(value);
    }

    public override void WriteLine()
    {
        Append(Environment.NewLine);
    }

    public override void WriteLine(string? value)
    {
        Append($"{value}{Environment.NewLine}");
    }

    public override void WriteLine(object? value)
    {
        Append($"{value}{Environment.NewLine}");
    }

    public override Encoding Encoding => Encoding.UTF8;

    private void Append(string? value)
    {
        bool scheduleUpdate;

        lock (_sync)
        {
            _content.Append(value);
            scheduleUpdate = Interlocked.Exchange(ref _updateScheduled, 1) == 0;
        }

        if (scheduleUpdate)
        {
            Dispatcher.UIThread.Post(FlushToUi);
        }
    }

    private void FlushToUi()
    {
        string content;

        lock (_sync)
        {
            content = _content.ToString();
            Volatile.Write(ref _updateScheduled, 0);
        }

        _searchLogHolder.SearchLog = content;
    }
}
