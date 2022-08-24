using System.IO;
using System.Security.Cryptography;

namespace DuplicateAssistant.Business.FileHashing;

public class Md5FileHashing : IHashFile
{
    public byte[] Hash(string path)
    {
        using MD5 md5 = MD5.Create();
        using FileStream fs = File.OpenRead(path);
        return md5.ComputeHash(fs);
    }
}