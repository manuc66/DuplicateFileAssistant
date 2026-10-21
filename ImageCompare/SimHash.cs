using System.Collections.Concurrent;
using System.Numerics;

namespace ImageCompare;

internal readonly record struct SimHash128(ulong Low, ulong High)
{
    public int DistanceTo(SimHash128 other)
    {
        return BitOperations.PopCount(Low ^ other.Low) + BitOperations.PopCount(High ^ other.High);
    }
}

/// <summary>
/// Random-hyperplane locality-sensitive hash. Cosine similarity between two
/// embeddings translates into a small Hamming distance, which lets the finder
/// shortlist candidates with a BK-tree instead of comparing every pair.
/// </summary>
internal static class SimHash
{
    public const int Bits = 128;

    private const uint Seed = 0x9E3779B9;
    private static readonly ConcurrentDictionary<int, float[][]> Hyperplanes = new();

    public static SimHash128 Compute(ReadOnlySpan<float> vector)
    {
        float[][] planes = Hyperplanes.GetOrAdd(vector.Length, CreateHyperplanes);

        ulong low = 0;
        ulong high = 0;

        for (int bit = 0; bit < Bits; bit++)
        {
            ReadOnlySpan<float> plane = planes[bit];
            float dot = 0;

            for (int index = 0; index < vector.Length; index++)
            {
                dot += vector[index] * plane[index];
            }

            if (dot >= 0)
            {
                if (bit < 64)
                {
                    low |= 1UL << bit;
                }
                else
                {
                    high |= 1UL << (bit - 64);
                }
            }
        }

        return new SimHash128(low, high);
    }

    private static float[][] CreateHyperplanes(int dimensions)
    {
        XorShift32 random = new(Seed ^ (uint)dimensions);
        float[][] planes = new float[Bits][];

        for (int bit = 0; bit < Bits; bit++)
        {
            float[] plane = new float[dimensions];

            for (int index = 0; index < dimensions; index++)
            {
                plane[index] = random.NextSignedFloat();
            }

            planes[bit] = plane;
        }

        return planes;
    }

    private struct XorShift32
    {
        private uint _state;

        public XorShift32(uint seed)
        {
            _state = seed == 0 ? 0x12345678u : seed;
        }

        public float NextSignedFloat()
        {
            uint value = Next();
            return value / (float)uint.MaxValue * 2f - 1f;
        }

        private uint Next()
        {
            uint value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }
    }
}
