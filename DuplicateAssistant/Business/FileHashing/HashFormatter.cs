using System;

namespace DuplicateAssistant.Business.FileHashing;

public static class HashFormatter
{
    public static string Format(ReadOnlySpan<byte> hash)
    {
        return Convert.ToHexStringLower(hash);
    }
}
