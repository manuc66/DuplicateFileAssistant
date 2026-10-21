using SkiaSharp;

namespace FileCompare.Tests;

internal static class TestImages
{
    public static SKBitmap CreatePattern(int width = 800, int height = 600)
    {
        SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.White);
        using SKPaint paint = new() { IsAntialias = true };

        paint.Color = new SKColor(30, 60, 120);
        canvas.DrawRect(0, 0, width * 0.45f, height, paint);

        paint.Color = new SKColor(200, 90, 40);
        canvas.DrawCircle(width * 0.7f, height * 0.35f, Math.Min(width, height) * 0.2f, paint);

        paint.Color = new SKColor(60, 160, 100);
        canvas.DrawRect(width * 0.55f, height * 0.6f, width * 0.3f, height * 0.3f, paint);

        using SKShader shader = SKShader.CreateLinearGradient(
            new SKPoint(0, height * 0.8f),
            new SKPoint(width, height),
            new[] { SKColors.Black, SKColors.White },
            SKShaderTileMode.Clamp);
        paint.Shader = shader;
        canvas.DrawRect(0, height * 0.8f, width, height * 0.2f, paint);

        paint.Shader = null;
        paint.Color = new SKColor(240, 220, 40);
        canvas.DrawCircle(width * 0.12f, height * 0.85f, Math.Min(width, height) * 0.06f, paint);

        return bitmap;
    }

    public static SKBitmap CreateOtherPattern(int width = 800, int height = 600)
    {
        SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(new SKColor(20, 20, 20));
        using SKPaint paint = new() { IsAntialias = true };

        for (int index = 0; index < 24; index++)
        {
            paint.Color = new SKColor(
                (byte)(index * 10),
                (byte)(255 - index * 9),
                (byte)(index * 5));

            canvas.DrawCircle(
                40 + index * width / 26f,
                height * (0.2f + 0.6f * (index % 5) / 5f),
                18 + index * 2,
                paint);
        }

        paint.Color = SKColors.White;
        canvas.DrawRect(width * 0.05f, height * 0.05f, width * 0.2f, height * 0.1f, paint);

        return bitmap;
    }

    public static SKBitmap CreateStripesPattern(int width = 800, int height = 600)
    {
        SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(new SKColor(240, 240, 240));
        using SKPaint paint = new() { IsAntialias = true, StrokeWidth = 18 };

        for (int x = -height; x < width; x += 60)
        {
            paint.Color = new SKColor(20, 20, 20);
            canvas.DrawLine(x, 0, x + height, height, paint);
        }

        paint.Color = new SKColor(200, 40, 40);
        canvas.DrawRect(width * 0.6f, height * 0.1f, width * 0.3f, height * 0.3f, paint);

        return bitmap;
    }

    public static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        return source.Resize(
            new SKImageInfo(width, height, source.ColorType, source.AlphaType),
            new SKSamplingOptions(SKCubicResampler.Mitchell))!;
    }

    public static byte[] Encode(SKBitmap bitmap, bool png, int quality = 90)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = png
            ? image.Encode(SKEncodedImageFormat.Png, 100)
            : image.Encode(SKEncodedImageFormat.Jpeg, quality);

        return data.ToArray();
    }

    public static SKBitmap Rotate90(SKBitmap source)
    {
        SKBitmap result = new(new SKImageInfo(source.Height, source.Width, source.ColorType, source.AlphaType));
        using SKCanvas canvas = new(result);
        canvas.Clear(SKColors.White);
        canvas.Translate(result.Width, 0);
        canvas.RotateDegrees(90);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    public static SKBitmap MirrorX(SKBitmap source)
    {
        SKBitmap result = new(source.Info);
        using SKCanvas canvas = new(result);
        canvas.Clear(SKColors.White);
        canvas.Translate(result.Width, 0);
        canvas.Scale(-1, 1);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    public static SKBitmap MirrorY(SKBitmap source)
    {
        SKBitmap result = new(source.Info);
        using SKCanvas canvas = new(result);
        canvas.Clear(SKColors.White);
        canvas.Translate(0, result.Height);
        canvas.Scale(1, -1);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }

    /// <summary>
    /// Produces the pixels that a viewer should display for the given EXIF
    /// orientation, using only the basic canvas transforms.
    /// </summary>
    public static SKBitmap Orient(SKBitmap source, int orientation)
    {
        return orientation switch
        {
            2 => MirrorX(source),
            3 => RotateTwice(source),
            4 => MirrorY(source),
            5 => MirrorX(Rotate90(source)),
            6 => Rotate90(source),
            7 => MirrorY(Rotate90(source)),
            8 => RotateThreeTimes(source),
            _ => source.Copy()
        };
    }

    public static byte[] WithExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] tiff =
        {
            0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00,
            0x01, 0x00, 0x00, 0x00,
            (byte)(orientation & 0xFF), (byte)(orientation >> 8), 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        byte[] payload = new byte[6 + tiff.Length];
        "Exif\0\0"u8.CopyTo(payload);
        tiff.CopyTo(payload, 6);

        int length = payload.Length + 2;
        byte[] app1 = new byte[4 + payload.Length];
        app1[0] = 0xFF;
        app1[1] = 0xE1;
        app1[2] = (byte)(length >> 8);
        app1[3] = (byte)length;
        payload.CopyTo(app1, 4);

        byte[] result = new byte[jpeg.Length + app1.Length];
        result[0] = jpeg[0];
        result[1] = jpeg[1];
        app1.CopyTo(result, 2);
        jpeg.AsSpan(2).CopyTo(result.AsSpan(2 + app1.Length));
        return result;
    }

    private static SKBitmap RotateTwice(SKBitmap source)
    {
        using SKBitmap once = Rotate90(source);
        return Rotate90(once);
    }

    private static SKBitmap RotateThreeTimes(SKBitmap source)
    {
        using SKBitmap once = Rotate90(source);
        using SKBitmap twice = Rotate90(once);
        return Rotate90(twice);
    }
}
