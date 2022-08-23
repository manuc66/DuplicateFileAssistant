using System.Numerics;

public class ReadFileInChunksAndCompareVector : ReadIntoByteBufferInChunks
{
    public ReadFileInChunksAndCompareVector(string filePath01, string filePath02, int chunkSize)
        : base(filePath01, filePath02, chunkSize)
    {
    }
    public ReadFileInChunksAndCompareVector(FileInfo filePath01, FileInfo filePath02, int chunkSize = 0)
        : base(filePath01, filePath02, chunkSize)
    {
    }

    protected override bool OnCompare()
    {
        using FileStream fileStream = FileInfo1.OpenRead();
        using FileStream openRead = FileInfo2.OpenRead();
        return StreamAreEqual(fileStream, openRead);
    }

    private bool StreamAreEqual(in Stream stream1, in Stream stream2)
    {
        byte[] buffer1 = new byte[ChunkSize];
        byte[] buffer2 = new byte[ChunkSize];

        while (true)
        {
            int count1 = ReadIntoBuffer(stream1, buffer1);
            int count2 = ReadIntoBuffer(stream2, buffer2);

            if (count1 != count2)
            {
                return false;
            }

            if (count1 == 0)
            {
                return true;
            }

            int totalProcessed = 0;
            while (totalProcessed < buffer1.Length)
            {
                if (Vector.EqualsAll(new(buffer1, totalProcessed),
                        new Vector<byte>(buffer2, totalProcessed)) == false)
                {
                    return false;
                }

                totalProcessed += Vector<byte>.Count;
            }
        }
    }
}