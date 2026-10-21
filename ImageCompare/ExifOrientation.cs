using SkiaSharp;

namespace ImageCompare;

/// <summary>
/// Maps coordinates between the stored pixels of a JPEG and the upright image
/// described by its EXIF orientation.
/// </summary>
public static class ExifOrientation
{
    public static bool SwapsAxes(SKEncodedOrigin origin)
    {
        return origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;
    }

    public static (int X, int Y) MapToStored(SKEncodedOrigin origin, int x, int y, int storedWidth,
        int storedHeight)
    {
        return origin switch
        {
            SKEncodedOrigin.TopRight => (storedWidth - 1 - x, y),
            SKEncodedOrigin.BottomRight => (storedWidth - 1 - x, storedHeight - 1 - y),
            SKEncodedOrigin.BottomLeft => (x, storedHeight - 1 - y),
            SKEncodedOrigin.LeftTop => (y, x),
            SKEncodedOrigin.RightTop => (y, storedHeight - 1 - x),
            SKEncodedOrigin.RightBottom => (storedWidth - 1 - y, storedHeight - 1 - x),
            SKEncodedOrigin.LeftBottom => (storedWidth - 1 - y, x),
            _ => (x, y)
        };
    }
}
