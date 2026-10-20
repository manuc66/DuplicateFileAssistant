using System;
using System.Collections.Generic;

namespace DuplicateAssistant.Business.FileHashing;

public static class HashAlgorithmFactory
{
    public static IReadOnlyList<string> Names { get; } = new[] { "MD5", "Blake3", "XXHash" };

    public static IHashFile Create(string? name)
    {
        return name switch
        {
            "MD5" => new Md5FileHashing(),
            "XXHash" => new XXHashFileHashing(),
            "Blake3" => new Blake3FileHashing(),
            _ => new Blake3FileHashing()
        };
    }
}
