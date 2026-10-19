using System.Security.Cryptography;

public class NaiveFileComparer : FileComparer
{
    const int BYTES_TO_READ = sizeof(Int64);

    public NaiveFileComparer(string filePath01, string filePath02) : base(filePath01, filePath02)
    {
    }

    public NaiveFileComparer(FileInfo fileInfo01, FileInfo fileInfo02) : base(fileInfo01, fileInfo02)
    {
    }

    protected override bool OnCompare()
    {
        using FileStream fs1 = FileInfo1.OpenRead();
        using FileStream fs2 = FileInfo2.OpenRead();

        byte[] one = new byte[BYTES_TO_READ];
        byte[] two = new byte[BYTES_TO_READ];

        while (true)
        {
            int read1 = fs1.Read(one, 0, one.Length);
            int read2 = fs2.Read(two, 0, two.Length);

            if (read1 != read2)
            {
                return false;
            }

            if (read1 == 0)
            {
                return true;
            }

            if (!one.AsSpan(0, read1).SequenceEqual(two.AsSpan(0, read2)))
            {
                return false;
            }
        }
    }
}