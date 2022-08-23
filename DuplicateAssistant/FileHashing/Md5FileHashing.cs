using System.IO;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;

namespace DuplicateAssistant.FileHashing;

public class Md5FileHashing : IHashFile
{
    public byte[] Hash(string path)
    {
        using MD5 md5 = MD5.Create();
        using FileStream fs = File.OpenRead(path);
        return md5.ComputeHash(fs);
    }
}