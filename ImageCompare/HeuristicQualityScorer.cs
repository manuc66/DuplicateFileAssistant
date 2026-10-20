using SkiaSharp;

namespace ImageCompare;

/// <summary>
/// Model-free quality estimate based on Laplacian sharpness and resolution.
/// It is intentionally cheap and deterministic; a model-based scorer can be
/// substituted through <see cref="IQualityScorer"/>.
/// </summary>
public sealed class HeuristicQualityScorer : IQualityScorer
{
    private const int MaximumSide = 1024;
    private const int AnalysisSide = 512;
    private const long MaximumPixels = 50_000_000;

    public string Id => "heuristic";

    public double Score(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = File.OpenRead(path);
        using SKCodec codec = SKCodec.Create(stream)
                              ?? throw new InvalidDataException($"Unsupported or corrupt image: {path}");

        SKImageInfo sourceInfo = codec.Info;

        if ((long)sourceInfo.Width * sourceInfo.Height > MaximumPixels)
        {
            throw new InvalidDataException(
                $"Image is too large to score ({sourceInfo.Width}x{sourceInfo.Height}): {path}");
        }

        float requestedScale = Math.Min(1f, MaximumSide / (float)Math.Max(sourceInfo.Width, sourceInfo.Height));
        SKSizeI decodedSize = codec.GetScaledDimensions(requestedScale);
        int width = Math.Max(1, decodedSize.Width);
        int height = Math.Max(1, decodedSize.Height);

        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKBitmap decoded = new(info);

        SKCodecResult result = codec.GetPixels(info, decoded.GetPixels());

        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new InvalidDataException($"Unable to decode image ({result}): {path}");
        }

        using SKBitmap bitmap = width > MaximumSide || height > MaximumSide
            ? decoded.Resize(
                new SKImageInfo(
                    Math.Max(1, Math.Min(width, MaximumSide)),
                    Math.Max(1, Math.Min(height, MaximumSide))),
                new SKSamplingOptions(SKCubicResampler.Mitchell))!
            : decoded.Copy();

        // Sharpness must be measured at a common scale, otherwise a small but
        // crisp thumbnail beats a large detailed original.
        float analysisScale = AnalysisSide / (float)Math.Max(bitmap.Width, bitmap.Height);
        int analysisWidth = Math.Max(1, (int)MathF.Round(bitmap.Width * analysisScale));
        int analysisHeight = Math.Max(1, (int)MathF.Round(bitmap.Height * analysisScale));

        using SKBitmap analysis = analysisWidth == bitmap.Width && analysisHeight == bitmap.Height
            ? bitmap.Copy()
            : bitmap.Resize(
                new SKImageInfo(analysisWidth, analysisHeight),
                new SKSamplingOptions(SKCubicResampler.Mitchell))!;

        byte[] gray = ToGray(analysis);
        double sharpness = Sharpness(gray, analysis.Width, analysis.Height);
        double resolution = Math.Clamp(
            (Math.Log2(Math.Max(sourceInfo.Width, sourceInfo.Height)) - 7.0) / 5.0,
            0.0,
            1.0);

        return 0.7 * sharpness + 0.3 * resolution;
    }

    public void Dispose()
    {
    }

    private static byte[] ToGray(SKBitmap bitmap)
    {
        ReadOnlySpan<byte> pixels = bitmap.GetPixelSpan();
        int stride = bitmap.RowBytes;
        byte[] gray = new byte[bitmap.Width * bitmap.Height];

        for (int y = 0; y < bitmap.Height; y++)
        {
            int row = y * stride;

            for (int x = 0; x < bitmap.Width; x++)
            {
                int index = row + x * 4;
                byte red = pixels[index];
                byte green = pixels[index + 1];
                byte blue = pixels[index + 2];
                byte alpha = pixels[index + 3];

                int r = red + 255 - alpha;
                int g = green + 255 - alpha;
                int b = blue + 255 - alpha;

                gray[y * bitmap.Width + x] = (byte)((77 * r + 150 * g + 29 * b) >> 8);
            }
        }

        return gray;
    }

    private static double Sharpness(ReadOnlySpan<byte> gray, int width, int height)
    {
        if (width < 3 || height < 3)
        {
            return 0;
        }

        double sum = 0;
        int count = 0;

        for (int y = 1; y < height - 1; y++)
        {
            int row = y * width;

            for (int x = 1; x < width - 1; x++)
            {
                int index = row + x;
                int laplacian = 4 * gray[index]
                                - gray[index - 1]
                                - gray[index + 1]
                                - gray[index - width]
                                - gray[index + width];

                sum += laplacian * laplacian;
                count++;
            }
        }

        double variance = sum / count;
        return variance / (variance + 400.0);
    }
}
