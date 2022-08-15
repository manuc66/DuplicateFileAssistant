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
        int iterations = (int)Math.Ceiling((double)FileInfo1.Length / BYTES_TO_READ);

        using (FileStream fs1 = FileInfo1.OpenRead())
        using (FileStream fs2 = FileInfo1.OpenRead())
        {
            byte[] one = new byte[BYTES_TO_READ];
            byte[] two = new byte[BYTES_TO_READ];

            for (int i = 0; i < iterations; i++)
            {
                fs1.Read(one, 0, BYTES_TO_READ);
                fs2.Read(two, 0, BYTES_TO_READ);

                if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                    return false;
            }
        }

        return true;
    }
}