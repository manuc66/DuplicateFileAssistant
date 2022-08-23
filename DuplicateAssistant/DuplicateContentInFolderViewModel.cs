using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using DuplicateAssistant.FileHashing;
using FileCompare;

namespace DuplicateAssistant;

public class DuplicateContentInFolderViewModel : DuplicateInFolderViewModel
{
    public DuplicateContentInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler) :
        base(trash, searchPath, fileManagerHandler)
    {
    }

    protected override Dictionary<string, HashSet<string>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        ControlWriter textDisplayProgress = new(this);

        XXHashFileHashing md5FileHashing = new();
        
        Func<string, string> hash = p => Hash(p, md5FileHashing, textDisplayProgress);

        return DupProvider.FindDuplicateByHash(SearchPath, searchOption, textDisplayProgress,
            progress => { ProgressValue = progress; }, hash, ct);
    }

    static string Hash(string path, IHashFile hashFile, TextWriter textDisplayProgress)
    {
        textDisplayProgress.Write($"Hashing {path}... ");
        byte[] retVal = hashFile.Hash(path);
        textDisplayProgress.WriteLine(FormatHash(retVal));
        return FormatHash(retVal);
    }

    private static string FormatHash(byte[] retVal)
    {
        StringBuilder sb = new();
        foreach (byte val in retVal)
        {
            sb.Append(val.ToString("x2"));
        }

        string hash = sb.ToString();
        return hash;
    }
}