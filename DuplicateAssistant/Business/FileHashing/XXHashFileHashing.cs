using System;
using System.IO;
using Blake3;
using HashDepot;

namespace DuplicateAssistant.Business.FileHashing;

public class XXHashFileHashing : IHashFile
{
    private readonly int _bufferSize;

    public XXHashFileHashing(int bufferSize = 4096 * 32)
    {
        _bufferSize = bufferSize;
    }

    public byte[] Hash(string path)
    {
        using Hasher hasher = Hasher.New();
        using FileStream fs = File.OpenRead(path);
       
        return BitConverter.GetBytes(XXHash.Hash64(fs));
    }
}