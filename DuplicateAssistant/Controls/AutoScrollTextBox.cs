using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DuplicateAssistant.Controls;

public class AutoScrollTextBox : TextBox
{
    private bool _scrollScheduled;
    private bool _shouldAutoScroll = true;

    public AutoScrollTextBox()
    {
        TextChanged += (_, _) => ScheduleScrollToEnd();
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    private void ScheduleScrollToEnd()
    {
        if (_scrollScheduled)
        {
            return;
        }

        ScrollViewer? scrollViewer = this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        _shouldAutoScroll = scrollViewer == null || IsAtBottom(scrollViewer);
        _scrollScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scrollScheduled = false;

            if (_shouldAutoScroll)
            {
                ScrollToEnd();
            }
        });
    }

    private void ScrollToEnd()
    {
        if (SelectionStart != SelectionEnd)
        {
            return;
        }

        ScrollViewer? scrollViewer = this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        string? text = Text;
        if (text == null)
        {
            return;
        }

        CaretIndex = text.Length;
        SelectionStart = text.Length;
        SelectionEnd = text.Length;

        if (scrollViewer != null)
        {
            scrollViewer.Offset = new Vector(
                scrollViewer.Offset.X,
                Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));
        }
    }

    private static bool IsAtBottom(ScrollViewer scrollViewer)
    {
        return scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 1;
    }
}
