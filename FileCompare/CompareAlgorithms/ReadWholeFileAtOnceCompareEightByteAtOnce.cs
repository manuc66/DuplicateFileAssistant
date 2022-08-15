public class ReadWholeFileAtOnceCompareEightByteAtOnce : FileComparer
{
    public ReadWholeFileAtOnceCompareEightByteAtOnce(string filePath01, string filePath02) : base(filePath01,
        filePath02)
    {
    }
    public ReadWholeFileAtOnceCompareEightByteAtOnce(FileInfo filePath01, FileInfo filePath02) : base(filePath01,
        filePath02)
    {
    }

    protected override bool OnCompare()
    {
        byte[] fileContents01 = File.ReadAllBytes(FileInfo1.FullName);
        byte[] fileContents02 = File.ReadAllBytes(FileInfo2.FullName);

        int lastBlockIndex = fileContents01.Length - (fileContents01.Length % sizeof(ulong));

        int totalProcessed = 0;
        while (totalProcessed < lastBlockIndex)
        {
            if (BitConverter.ToUInt64(fileContents01, totalProcessed) !=
                BitConverter.ToUInt64(fileContents02, totalProcessed))
            {
                return false;
            }

            totalProcessed += sizeof(ulong);
        }

        return true;
    }
}