using ImageCompare;
using SkiaSharp;

namespace ImageCompare.Onnx;

internal static class OnnxImagePreprocessor
{
    private const long MaximumDecodedPixels = 40_000_000;
    private const float CenterCropRatio = 256f / 224f;

    public static float[] Load(string path, OnnxImageEmbeddingModel model)
    {
        return Load(path, model.InputSize, model.ResizeMode, model.Mean, model.StandardDeviation);
    }

    public static float[] Load(string path, OnnxQualityModel model)
    {
        return Load(path, model.InputSize, ImageResizeMode.Stretch, model.Mean,
            model.StandardDeviation);
    }

    private static float[] Load(string path, int targetSize, ImageResizeMode resizeMode, float[] mean,
        float[] standardDeviation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = File.OpenRead(path);
        using SKCodec codec = SKCodec.Create(stream)
                              ?? throw new InvalidDataException($"Unsupported or corrupt image: {path}");

        SKImageInfo sourceInfo = codec.Info;

        if ((long)sourceInfo.Width * sourceInfo.Height > MaximumDecodedPixels)
        {
            throw new InvalidDataException(
                $"Image is too large to embed ({sourceInfo.Width}x{sourceInfo.Height}): {path}");
        }

        int targetShortestSide = resizeMode == ImageResizeMode.ShortestSideCenterCrop
            ? (int)MathF.Round(targetSize * CenterCropRatio)
            : targetSize;

        float scale = Math.Min(1f, targetShortestSide * 2f /
                                   Math.Max(sourceInfo.Width, sourceInfo.Height));

        SKSizeI decodedSize = codec.GetScaledDimensions(scale);
        int decodedWidth = Math.Max(1, decodedSize.Width);
        int decodedHeight = Math.Max(1, decodedSize.Height);

        if ((long)decodedWidth * decodedHeight > MaximumDecodedPixels)
        {
            throw new InvalidDataException(
                $"Decoded image is too large to embed ({decodedWidth}x{decodedHeight}): {path}");
        }

        SKImageInfo info = new(decodedWidth, decodedHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKBitmap decoded = new(info);

        SKCodecResult result = codec.GetPixels(info, decoded.GetPixels());

        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new InvalidDataException($"Unable to decode image ({result}): {path}");
        }

        using SKBitmap upright = ApplyOrientation(decoded, codec.EncodedOrigin);
        using SKBitmap resized = ResizeAndCrop(upright, targetSize, resizeMode);

        return ToTensor(resized, mean, standardDeviation);
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
        SKBitmap destination = new(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));

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

    private static SKBitmap ResizeAndCrop(SKBitmap source, int targetSize, ImageResizeMode resizeMode)
    {
        if (resizeMode == ImageResizeMode.Stretch)
        {
            return source.Resize(
                new SKImageInfo(targetSize, targetSize, SKColorType.Rgba8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
                ?? throw new InvalidOperationException("Unable to resize the image.");
        }

        int shortestSide = (int)MathF.Round(targetSize * CenterCropRatio);
        float scale = shortestSide / (float)Math.Min(source.Width, source.Height);
        int resizedWidth = Math.Max(targetSize, (int)MathF.Round(source.Width * scale));
        int resizedHeight = Math.Max(targetSize, (int)MathF.Round(source.Height * scale));

        using SKBitmap resized = source.Resize(
            new SKImageInfo(resizedWidth, resizedHeight, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException("Unable to resize the image.");

        int cropX = (resizedWidth - targetSize) / 2;
        int cropY = (resizedHeight - targetSize) / 2;

        SKBitmap destination = new(
            new SKImageInfo(targetSize, targetSize, SKColorType.Rgba8888, SKAlphaType.Premul));

        using (SKCanvas canvas = new(destination))
        {
            canvas.DrawBitmap(
                resized,
                new SKRect(cropX, cropY, cropX + targetSize, cropY + targetSize),
                new SKRect(0, 0, targetSize, targetSize),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
        }

        return destination;
    }

    private static float[] ToTensor(SKBitmap bitmap, float[] mean, float[] standardDeviation)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        int plane = width * height;
        float[] tensor = new float[3 * plane];
        ReadOnlySpan<byte> pixels = bitmap.GetPixelSpan();
        int stride = bitmap.RowBytes;

        for (int y = 0; y < height; y++)
        {
            int row = y * stride;

            for (int x = 0; x < width; x++)
            {
                int index = row + x * 4;
                byte red = pixels[index];
                byte green = pixels[index + 1];
                byte blue = pixels[index + 2];
                byte alpha = pixels[index + 3];

                // Premultiplied pixel composited on white.
                int r = red + 255 - alpha;
                int g = green + 255 - alpha;
                int b = blue + 255 - alpha;
                int pixel = y * width + x;

                tensor[pixel] = (r / 255f - mean[0]) / standardDeviation[0];
                tensor[plane + pixel] = (g / 255f - mean[1]) / standardDeviation[1];
                tensor[2 * plane + pixel] = (b / 255f - mean[2]) / standardDeviation[2];
            }
        }

        return tensor;
    }
}
