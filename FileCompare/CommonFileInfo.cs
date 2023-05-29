namespace FileCompare;

public class CommonFileInfo : IFileInfo
{
    private readonly FileInfo _fileInfo;
    public CommonFileInfo(string path)
    {
        _fileInfo= new FileInfo(path);
    }

    public long Length => _fileInfo.Length;

    public bool IsLink()
    {
        return _fileInfo.LinkTarget != null;
    }

    public bool IsFifoFile()
    {
        return false;
    }
}