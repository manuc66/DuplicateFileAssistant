namespace FileCompare;

public interface IFileInfo
{
    long Length { get; }
    bool IsLink();
    bool IsFifoFile();
}