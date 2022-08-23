﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using DuplicateAssistant.FileHashing;
using FileCompare;
using ReactiveUI;

namespace DuplicateAssistant;

public class DuplicateContentInFolderViewModel : DuplicateInFolderViewModel
{
        
    private List<string> _hashAlgorithms;

    public List<string> HashAlgorithms
    {
        get => _hashAlgorithms;
        set => this.RaiseAndSetIfChanged(ref _hashAlgorithms, value);
    }
    private string _hashAlgorithm;
    public string HashAlgorithm
    {
        get => _hashAlgorithm;
        set => this.RaiseAndSetIfChanged(ref _hashAlgorithm, value);
    }
    
    public DuplicateContentInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler) :
        base(trash, searchPath, fileManagerHandler)
    {
        HashAlgorithms = new List<string> { "MD5", "Blake3", "XXHash" };
        HashAlgorithm = HashAlgorithms[1];
    }

    protected override Dictionary<string, HashSet<string>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        ControlWriter textDisplayProgress = new(this);

        IHashFile fileHashing = GetFileHashing();

        Func<string, string> hash = p => Hash(p, fileHashing, textDisplayProgress);

        return DupProvider.FindDuplicateByHash(SearchPath, searchOption, textDisplayProgress,
            progress => { ProgressValue = progress; }, hash, ct);
    }

    private IHashFile GetFileHashing()
    {
        IHashFile fileHashing;
        switch (HashAlgorithm)
        {
            case "MD5":
                fileHashing = new Md5FileHashing();
                break;
            case "Blake3":
                fileHashing = new Blake3FileHashing();
                break;
            case "XXHash":
                fileHashing = new XXHashFileHashing();
                break;
            default:
                fileHashing = new XXHashFileHashing();
                break;
        }

        return fileHashing;
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