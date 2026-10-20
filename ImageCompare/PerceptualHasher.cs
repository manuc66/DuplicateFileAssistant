using System.Buffers.Binary;
using SkiaSharp;

namespace ImageCompare;

/// <summary>
/// Computes a 64-bit difference hash (dHash) that is stable across resizing and
/// recompression. Images are decoded at a reduced scale, alpha is composited on
/// white, EXIF orientation is applied, then a 9x8 grayscale grid is compared
/// horizontally.
/// </summary>
public static class PerceptualHasher
{
    public const string CacheAlgorithm = "dhash64-v1";
    public const int HashWidth = 9;
    public const int HashHeight = 8;
    public const int HashBits = 64;
    public const int CacheBytes = 16;
    public const long MaximumDecodedPixels = 50_000_000;

    private const float PredecodeScale = 0.125f;

    public static PerceptualHash Compute(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = File.OpenRead(path);
        using SKCodec codec = SKCodec.Create(stream)
                              ?? throw new InvalidDataException($"Unsupported or corrupt image: {path}");

        SKSizeI decodedSize = codec.GetScaledDimensions(PredecodeScale);
        int decodedWidth = Math.Max(1, decodedSize.Width);
        int decodedHeight = Math.Max(1, decodedSize.Height);

        if ((long)decodedWidth * decodedHeight > MaximumDecodedPixels)
        {
            throw new InvalidDataException(
                $"Image is too large to hash ({decodedWidth}x{decodedHeight}): {path}");
        }

        // Premultiplied RGBA lets us composite transparency on white reliably.
        SKImageInfo info = new(decodedWidth, decodedHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKBitmap bitmap = new(info);

        SKCodecResult result = codec.GetPixels(info, bitmap.GetPixels());

        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new InvalidDataException($"Unable to decode image ({result}): {path}");
        }

        SKEncodedOrigin origin = codec.EncodedOrigin;
        bool swapsAxes = ExifOrientation.SwapsAxes(origin);
        int gridWidth = swapsAxes ? HashHeight : HashWidth;
        int gridHeight = swapsAxes ? HashWidth : HashHeight;

        Span<int> grid = stackalloc int[HashWidth * HashHeight];
        BuildLumaGrid(bitmap, gridWidth, gridHeight, grid);

        Span<int> upright = stackalloc int[HashWidth * HashHeight];
        ApplyOrientation(origin, grid, gridWidth, gridHeight, upright);

        ulong hash = 0;
        int bit = 0;

        for (int y = 0; y < HashHeight; y++)
        {
            for (int x = 0; x < HashHeight; x++, bit++)
            {
                if (upright[y * HashWidth + x] < upright[y * HashWidth + x + 1])
                {
                    hash |= 1UL << bit;
                }
            }
        }

        int width = swapsAxes ? codec.Info.Height : codec.Info.Width;
        int height = swapsAxes ? codec.Info.Width : codec.Info.Height;

        return new PerceptualHash(hash, width, height);
    }

    public static byte[] ToCacheBytes(PerceptualHash hash)
    {
        byte[] bytes = new byte[CacheBytes];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, hash.Value);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(sizeof(ulong)), hash.Width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(sizeof(ulong) + sizeof(int)), hash.Height);
        return bytes;
    }

    public static bool TryFromCacheBytes(ReadOnlySpan<byte> bytes, out PerceptualHash hash)
    {
        hash = default;

        if (bytes.Length != CacheBytes)
        {
            return false;
        }

        ulong value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        int width = BinaryPrimitives.ReadInt32LittleEndian(bytes[sizeof(ulong)..]);
        int height = BinaryPrimitives.ReadInt32LittleEndian(bytes[(sizeof(ulong) + sizeof(int))..]);

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        hash = new PerceptualHash(value, width, height);
        return true;
    }

    private static void BuildLumaGrid(SKBitmap bitmap, int targetWidth, int targetHeight, Span<int> grid)
    {
        ReadOnlySpan<byte> pixels = bitmap.GetPixelSpan();
        int stride = bitmap.RowBytes;
        int sourceWidth = bitmap.Width;
        int sourceHeight = bitmap.Height;

        for (int y = 0; y < targetHeight; y++)
        {
            int y0 = Math.Min(sourceHeight - 1, y * sourceHeight / targetHeight);
            int y1 = Math.Min(sourceHeight, Math.Max(y0 + 1, (y + 1) * sourceHeight / targetHeight));

            for (int x = 0; x < targetWidth; x++)
            {
                int x0 = Math.Min(sourceWidth - 1, x * sourceWidth / targetWidth);
                int x1 = Math.Min(sourceWidth, Math.Max(x0 + 1, (x + 1) * sourceWidth / targetWidth));

                int sum = 0;
                int count = 0;

                for (int sourceY = y0; sourceY < y1; sourceY++)
                {
                    int row = sourceY * stride;

                    for (int sourceX = x0; sourceX < x1; sourceX++)
                    {
                        int index = row + sourceX * 4;
                        byte red = pixels[index];
                        byte green = pixels[index + 1];
                        byte blue = pixels[index + 2];
                        byte alpha = pixels[index + 3];

                        // Premultiplied pixel composited on a white background.
                        int r = red + 255 - alpha;
                        int g = green + 255 - alpha;
                        int b = blue + 255 - alpha;

                        sum += (77 * r + 150 * g + 29 * b) >> 8;
                        count++;
                    }
                }

                grid[y * targetWidth + x] = (sum + count / 2) / count;
            }
        }
    }

    private static void ApplyOrientation(SKEncodedOrigin origin, ReadOnlySpan<int> grid, int gridWidth,
        int gridHeight, Span<int> upright)
    {
        for (int y = 0; y < HashHeight; y++)
        {
            for (int x = 0; x < HashWidth; x++)
            {
                (int sourceX, int sourceY) = ExifOrientation.MapToStored(origin, x, y, gridWidth, gridHeight);
                upright[y * HashWidth + x] = grid[sourceY * gridWidth + sourceX];
            }
        }
    }
}
