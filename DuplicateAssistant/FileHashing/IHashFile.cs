using System;

namespace DuplicateAssistant.FileHashing;

public interface IHashFile
{
    byte[] Hash(string path);
}