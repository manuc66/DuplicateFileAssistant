public class ReadWholeFileAtOnce : FileComparer
{
    public ReadWholeFileAtOnce(string filePath01, string filePath02) : base(filePath01, filePath02)
    {
    }
    public ReadWholeFileAtOnce(FileInfo filePath01, FileInfo filePath02) : base(filePath01, filePath02)
    {
    }

    protected override bool OnCompare()
    {
        byte[] fileContents01 = File.ReadAllBytes(FileInfo1.FullName);
        byte[] fileContents02 = File.ReadAllBytes(FileInfo2.FullName);
        for (int i = 0; i < fileContents01.Length; i++)
        {
            if (fileContents01[i] != fileContents02[i])
            {
                return false;
            }
        }

        return true;
    }
}