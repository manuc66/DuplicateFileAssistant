using System;
using SkiaSharp;

namespace DuplicateAssistant.Business;

/// <summary>
/// Approximate visual difference between two pictures. Both are normalized to
/// the same small size and compared pixel by pixel. It is meaningful for the same
/// framing (resize, recompression); a crop or rotation needs alignment and will
/// show as a large difference.
/// </summary>
internal static class ImageDifference
{
    public const int Width = 256;
    public const int Height = 256;

    private const int Amplification = 4;

    internal sealed record Result(byte[] Png, double MeanDifference);

    public static Result? Create(string firstPath, string secondPath)
    {
        byte[]? firstPng = ThumbnailLoader.LoadPng(firstPath, 512, 512);
        byte[]? secondPng = ThumbnailLoader.LoadPng(secondPath, 512, 512);

        if (firstPng is null || secondPng is null)
        {
            return null;
        }

        using SKBitmap? first = SKBitmap.Decode(firstPng);
        using SKBitmap? second = SKBitmap.Decode(secondPng);

        if (first is null || second is null)
        {
            return null;
        }

        using SKBitmap normalizedFirst = Resize(first);
        using SKBitmap normalizedSecond = Resize(second);
        using SKBitmap difference = new(
            new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));

        long total = 0;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                (int firstRed, int firstGreen, int firstBlue) = OverWhite(normalizedFirst.GetPixel(x, y));
                (int secondRed, int secondGreen, int secondBlue) = OverWhite(normalizedSecond.GetPixel(x, y));

                int red = Math.Abs(firstRed - secondRed);
                int green = Math.Abs(firstGreen - secondGreen);
                int blue = Math.Abs(firstBlue - secondBlue);
                int value = Math.Min(255, (red + green + blue) / 3 * Amplification);

                difference.SetPixel(x, y, new SKColor((byte)value, 0, 0, 255));
                total += value;
            }
        }

        using SKImage image = SKImage.FromBitmap(difference);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 90);

        return new Result(data.ToArray(), total / (double)(Width * Height));
    }

    private static SKBitmap Resize(SKBitmap source)
    {
        if (source.Width == Width && source.Height == Height)
        {
            return source.Copy();
        }

        return source.Resize(
            new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? source.Copy();
    }

    private static (int Red, int Green, int Blue) OverWhite(SKColor color)
    {
        return (
            color.Red + 255 - color.Alpha,
            color.Green + 255 - color.Alpha,
            color.Blue + 255 - color.Alpha);
    }
}
