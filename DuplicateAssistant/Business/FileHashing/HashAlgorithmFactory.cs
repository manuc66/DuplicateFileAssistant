using System;
using System.Collections.Generic;

namespace DuplicateAssistant.Business.FileHashing;

public static class HashAlgorithmFactory
{
    public static IReadOnlyList<string> Names { get; } = new[] { "MD5", "Blake3", "XXHash" };

    public static IHashFile Create(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new Blake3FileHashing();
        }

        return name.Trim().ToLowerInvariant() switch
        {
            "md5" => new Md5FileHashing(),
            "xxhash" => new XXHashFileHashing(),
            "blake3" => new Blake3FileHashing(),
            _ => throw new ArgumentException(
                $"Unknown hash algorithm: {name}. Supported algorithms: {string.Join(", ", Names)}.",
                nameof(name))
        };
    }
}
