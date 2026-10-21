using System;
using System.IO;
using ImageCompare;
using SkiaSharp;

namespace DuplicateAssistant.Business;

/// <summary>
/// Decodes a small PNG thumbnail without loading the full-resolution picture
/// when the codec supports scaled decoding. Returns raw PNG bytes so the
/// Avalonia bitmap can be created on the UI thread.
/// </summary>
internal static class ThumbnailLoader
{
    private const long MaximumSourcePixels = 40_000_000;

    public static byte[]? LoadPng(string path, int maximumWidth, int maximumHeight)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using SKCodec codec = SKCodec.Create(stream);

            if (codec is null)
            {
                return null;
            }

            SKImageInfo sourceInfo = codec.Info;

            if ((long)sourceInfo.Width * sourceInfo.Height > MaximumSourcePixels)
            {
                return null;
            }

            float scale = Math.Min(
                1f,
                Math.Min(
                    maximumWidth / (float)sourceInfo.Width,
                    maximumHeight / (float)sourceInfo.Height));

            SKSizeI decodedSize = codec.GetScaledDimensions(scale);
            int width = Math.Max(1, decodedSize.Width);
            int height = Math.Max(1, decodedSize.Height);

            SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using SKBitmap decoded = new(info);

            SKCodecResult result = codec.GetPixels(info, decoded.GetPixels());

            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                return null;
            }

            using SKBitmap upright = ApplyOrientation(decoded, codec.EncodedOrigin);
            using SKBitmap resized = Resize(upright, maximumWidth, maximumHeight);
            using SKImage image = SKImage.FromBitmap(resized);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 90);

            return data.ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static SKBitmap Resize(SKBitmap source, int maximumWidth, int maximumHeight)
    {
        float scale = Math.Min(
            1f,
            Math.Min(
                maximumWidth / (float)source.Width,
                maximumHeight / (float)source.Height));

        int width = Math.Max(1, (int)MathF.Round(source.Width * scale));
        int height = Math.Max(1, (int)MathF.Round(source.Height * scale));

        if (width == source.Width && height == source.Height)
        {
            return source.Copy();
        }

        return source.Resize(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? source.Copy();
    }

    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
        {
            return source.Copy();
        }

        bool swapsAxes = ExifOrientation.SwapsAxes(origin);
        int width = swapsAxes ? source.Height : source.Width;
        int height = swapsAxes ? source.Width : source.Height;
        SKBitmap destination = new(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));

        ReadOnlySpan<byte> sourcePixels = source.GetPixelSpan();
        Span<byte> destinationPixels = destination.GetPixelSpan();
        int sourceStride = source.RowBytes;
        int destinationStride = destination.RowBytes;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                (int sourceX, int sourceY) = ExifOrientation.MapToStored(
                    origin,
                    x,
                    y,
                    source.Width,
                    source.Height);

                int sourceOffset = sourceY * sourceStride + sourceX * 4;
                int destinationOffset = y * destinationStride + x * 4;

                sourcePixels.Slice(sourceOffset, 4).CopyTo(destinationPixels.Slice(destinationOffset, 4));
            }
        }

        return destination;
    }
}
