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
        HashAlgorithms = new List<string>(HashAlgorithmFactory.Names);
        HashAlgorithm = "Blake3";
    }

    protected override Dictionary<HashKey, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        using ControlWriter textDisplayProgress = new(this);

        IHashFile fileHashing = HashAlgorithmFactory.Create(HashAlgorithm);
        FileSearchFilter searchFilter = CreateSearchFilter();

        Func<string, byte[]> hash = p => Hash(p, fileHashing, textDisplayProgress, ct);

        return DupProvider.FindDuplicateByHash(SearchPath, searchOption, searchFilter.MatchesFile, textDisplayProgress,
            UpdateProgress, hash, ct, searchFilter.MatchesPath);
    }

    static byte[] Hash(string path, IHashFile hashFile, TextWriter textDisplayProgress, CancellationToken cancellationToken)
    {
        textDisplayProgress.Write($"Hashing {path}... ");
        byte[] retVal = hashFile.Hash(path, cancellationToken);
        textDisplayProgress.WriteLine(HashFormatter.Format(retVal));
        return retVal;
    }
}