using System.Security.Cryptography;

public class Md5Comparer : FileComparer
{
    public Md5Comparer(string filePath01, string filePath02) : base(filePath01, filePath02)
    {
    }
    public Md5Comparer(FileInfo filePath01, FileInfo filePath02) : base(filePath01, filePath02)
    {
    }

    protected override bool OnCompare()
    {
        using FileStream fileStream01 = FileInfo1.OpenRead();
        using FileStream fileStream02 = FileInfo2.OpenRead();
        using MD5 md5Creator = MD5.Create();

        byte[] fileStream01Hash = md5Creator.ComputeHash(fileStream01);
        byte[] fileStream02Hash = md5Creator.ComputeHash(fileStream02);

        for (int i = 0; i < fileStream01Hash.Length; i++)
        {
            if (fileStream01Hash[i] != fileStream02Hash[i])
            {
                return false;
            }
        }

        return true;
    }
}