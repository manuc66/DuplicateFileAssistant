using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace DuplicateAssistant.Business.FileHashing;

public class Md5FileHashing : IHashFile
{
    private const int BufferSize = 4096 * 32;

    public byte[] Hash(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using FileStream fs = File.OpenRead(path);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

            for (int read; (read = fs.Read(buffer, 0, buffer.Length)) != 0;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, read);
            }

            return hash.GetHashAndReset();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
