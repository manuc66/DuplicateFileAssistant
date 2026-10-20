using System.Numerics;

namespace ImageCompare;

/// <summary>
/// 64-bit difference hash of an image, together with its upright dimensions.
/// Images that only differ by scaling or compression usually stay within a few bits.
/// </summary>
public readonly record struct PerceptualHash(ulong Value, int Width, int Height)
{
    public const double DefaultAspectRatioTolerance = 0.08;

    public double AspectRatio => Height > 0 ? (double)Width / Height : 0;

    public int DistanceTo(PerceptualHash other)
    {
        return BitOperations.PopCount(Value ^ other.Value);
    }

    public bool HasSimilarAspectRatio(PerceptualHash other,
        double tolerance = DefaultAspectRatioTolerance)
    {
        double left = AspectRatio;
        double right = other.AspectRatio;

        if (left <= 0 || right <= 0)
        {
            return false;
        }

        return Math.Abs(left - right) <= tolerance * Math.Max(left, right);
    }
}
