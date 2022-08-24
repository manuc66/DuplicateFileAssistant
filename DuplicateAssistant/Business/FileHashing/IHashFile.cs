namespace DuplicateAssistant.Business.FileHashing;

public interface IHashFile
{
    byte[] Hash(string path);
}