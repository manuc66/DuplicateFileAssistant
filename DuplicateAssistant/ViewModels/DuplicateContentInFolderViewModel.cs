using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DuplicateAssistant.Business;
using DuplicateAssistant.Business.FileHashing;
using FileCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class DuplicateContentInFolderViewModel : DuplicateInFolderViewModel
{
    private List<string> _hashAlgorithms = null!;

    public List<string> HashAlgorithms
    {
        get => _hashAlgorithms;
        set => this.RaiseAndSetIfChanged(ref _hashAlgorithms, value);
    }

    private string _hashAlgorithm = null!;

    public string HashAlgorithm
    {
        get => _hashAlgorithm;
        set => this.RaiseAndSetIfChanged(ref _hashAlgorithm, value);
    }

    public DuplicateContentInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler,
        IDeleteConfirmation deleteConfirmation) :
        base(trash, searchPath, fileManagerHandler, deleteConfirmation)
    {
        HashAlgorithms = new List<string> { "MD5", "Blake3", "XXHash" };
        HashAlgorithm = HashAlgorithms[1];
    }

    protected override Dictionary<HashKey, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        using ControlWriter textDisplayProgress = new(this);

        IHashFile fileHashing = GetFileHashing();
        FileSearchFilter searchFilter = CreateSearchFilter();

        Func<string, byte[]> hash = p => Hash(p, fileHashing, textDisplayProgress, ct);

        return DupProvider.FindDuplicateByHash(SearchPath, searchOption, searchFilter.MatchesFile, textDisplayProgress,
            UpdateProgress, hash, ct, searchFilter.MatchesPath);
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

    static byte[] Hash(string path, IHashFile hashFile, TextWriter textDisplayProgress, CancellationToken cancellationToken)
    {
        textDisplayProgress.Write($"Hashing {path}... ");
        byte[] retVal = hashFile.Hash(path, cancellationToken);
        textDisplayProgress.WriteLine(HashFormatter.Format(retVal));
        return retVal;
    }
}