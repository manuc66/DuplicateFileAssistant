using System.Buffers;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FileCompare;

public static class FastFileCompare
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static bool AreFilesEqual(FileInfo fileInfo1, FileInfo fileInfo2, int bufferSize = 4096 * 32)
    {
        if (fileInfo1.Exists == false)
        {
            throw new FileNotFoundException(nameof(fileInfo1), fileInfo1.FullName);
        }

        if (fileInfo2.Exists == false)
        {
            throw new FileNotFoundException(nameof(fileInfo2), fileInfo2.FullName);
        }

        if (fileInfo1.Length != fileInfo2.Length)
        {
            return false;
        }

        if (string.Equals(fileInfo1.FullName, fileInfo2.FullName, PathComparison))
        {
            return true;
        }

        using FileStream fileStream01 = fileInfo1.OpenRead();
        using FileStream fileStream02 = fileInfo2.OpenRead();
        ArrayPool<byte> sharedArrayPool = ArrayPool<byte>.Shared;
        byte[] buffer1 = sharedArrayPool.Rent(bufferSize);
        byte[] buffer2 = sharedArrayPool.Rent(bufferSize);
        int usableBufferLength = Math.Min(buffer1.Length, buffer2.Length);
        Array.Fill<byte>(buffer1, 0);
        Array.Fill<byte>(buffer2, 0);
        try
        {
            while (true)
            {
                int len1 = 0;
                for (int read;
                     len1 < usableBufferLength &&
                     (read = fileStream01.Read(buffer1, len1, usableBufferLength - len1)) != 0;
                     len1 += read)
                {
                }

                int len2 = 0;
                for (int read;
                     len2 < usableBufferLength &&
                     (read = fileStream02.Read(buffer2, len2, usableBufferLength - len2)) != 0;
                     len2 += read)
                {
                }

                if (len1 != len2)
                {
                    return false;
                }

                if (len1 == 0)
                {
                    return true;
                }

                if (!BuffersAreEqual(buffer1, buffer2, len1))
                {
                    return false;
                }
            }
        }
        finally
        {
            sharedArrayPool.Return(buffer1);
            sharedArrayPool.Return(buffer2);
        }
    }

    private static unsafe bool BuffersAreEqual(byte[] buffer1, byte[] buffer2, int length)
    {
        if (!Avx2.IsSupported)
        {
            return buffer1.AsSpan(0, length).SequenceEqual(buffer2.AsSpan(0, length));
        }

        fixed (byte* pb1 = buffer1)
        fixed (byte* pb2 = buffer2)
        {
            int vectorSize = Vector256<byte>.Count;
            int processed = 0;

            for (; processed + vectorSize <= length; processed += vectorSize)
            {
                Vector256<byte> result = Avx2.CompareEqual(
                    Avx.LoadVector256(pb1 + processed),
                    Avx.LoadVector256(pb2 + processed));

                if (Avx2.MoveMask(result) != -1)
                {
                    return false;
                }
            }

            return buffer1.AsSpan(processed, length - processed)
                .SequenceEqual(buffer2.AsSpan(processed, length - processed));
        }
    }
}