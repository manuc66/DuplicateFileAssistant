using System;
using System.Buffers;
using System.IO;
using System.Threading;
using Blake3;

namespace DuplicateAssistant.Business.FileHashing;

public class Blake3FileHashing : IHashFile
{
    private readonly int _bufferSize;

    public Blake3FileHashing(int bufferSize = 4096 * 32)
    {
        _bufferSize = bufferSize;
    }

    public byte[] Hash(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using Hasher hasher = Hasher.New();
        using FileStream fs = File.OpenRead(path);
        ArrayPool<byte> sharedArrayPool = ArrayPool<byte>.Shared;
        byte[] buffer = sharedArrayPool.Rent(_bufferSize);
        try
        {
            for (int read; (read = fs.Read(buffer, 0, buffer.Length)) != 0;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hasher.Update(buffer.AsSpan(start: 0, read));
            }

            Hash finalize = hasher.Finalize();
            return finalize.AsSpan().ToArray();
        }
        finally
        {
            sharedArrayPool.Return(buffer);
        }
    }
}