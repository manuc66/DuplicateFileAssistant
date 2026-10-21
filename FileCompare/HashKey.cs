namespace FileCompare;

public readonly struct HashKey : IEquatable<HashKey>
{
    private readonly byte[]? _bytes;
    private readonly int _discriminator;
    private readonly int _hashCode;

    public HashKey(ReadOnlySpan<byte> bytes, int discriminator = 0)
    {
        _bytes = bytes.ToArray();
        _discriminator = discriminator;
        _hashCode = ComputeHashCode(bytes, discriminator);
    }

    private HashKey(byte[] bytes, int discriminator)
    {
        _bytes = bytes;
        _discriminator = discriminator;
        _hashCode = ComputeHashCode(bytes, discriminator);
    }

    public static HashKey FromOwned(byte[] bytes)
    {
        return new HashKey(bytes, 0);
    }

    public HashKey WithDiscriminator(int discriminator)
    {
        return new HashKey(_bytes ?? Array.Empty<byte>(), discriminator);
    }

    public bool Equals(HashKey other)
    {
        return _discriminator == other._discriminator &&
               _bytes.AsSpan().SequenceEqual(other._bytes.AsSpan());
    }

    public override bool Equals(object? obj)
    {
        return obj is HashKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public override string ToString()
    {
        return _bytes is null ? string.Empty : Convert.ToHexStringLower(_bytes);
    }

    private static int ComputeHashCode(ReadOnlySpan<byte> bytes, int discriminator)
    {
        HashCode hashCode = new();
        hashCode.AddBytes(bytes);
        hashCode.Add(discriminator);
        return hashCode.ToHashCode();
    }
}
