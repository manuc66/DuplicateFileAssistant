using System;
using Avalonia;

namespace DuplicateAssistant.Business;

/// <summary>
/// Converts pointer drags into a clamped scroll offset. Kept separate from the
/// window so the panning arithmetic can be tested without a visual tree.
/// </summary>
internal sealed class PanState
{
    private Point _start;
    private Vector _offset;

    public void Begin(Point pointer, Vector offset)
    {
        _start = pointer;
        _offset = offset;
    }

    public Vector Move(Point pointer, Vector extent, Vector viewport)
    {
        Vector delta = pointer - _start;
        double maximumX = Math.Max(0, extent.X - viewport.X);
        double maximumY = Math.Max(0, extent.Y - viewport.Y);

        return new Vector(
            Math.Clamp(_offset.X - delta.X, 0, maximumX),
            Math.Clamp(_offset.Y - delta.Y, 0, maximumY));
    }
}
