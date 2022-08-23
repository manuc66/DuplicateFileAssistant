using System;
using System.Buffers;
using System.IO;
using Blake3;

namespace DuplicateAssistant.FileHashing;

public class Blake3FileHashing : IHashFile
{
    private readonly int _bufferSize;

    public Blake3FileHashing(int bufferSize = 4096 * 32)
    {
        _bufferSize = bufferSize;
    }

    public byte[] Hash(string path)
    {
        using Hasher hasher = Hasher.New();
        using FileStream fs = File.OpenRead(path);
        ArrayPool<byte> sharedArrayPool = ArrayPool<byte>.Shared;
        byte[] buffer = sharedArrayPool.Rent(_bufferSize);
        Array.Fill<byte>(buffer, 0);
        try
        {
            for (int read; (read = fs.Read(buffer, 0, buffer.Length)) != 0;)
            {
                hasher.Update(buffer.AsSpan(start: 0, read));
            }
            
            Hash finalize = hasher.Finalize();
            return finalize.AsSpanUnsafe().ToArray();
        }
        finally
        {
            sharedArrayPool.Return(buffer);
        }
    }
}