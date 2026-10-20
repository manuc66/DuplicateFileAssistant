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
    private readonly ScanCache? _scanCache;

    public DuplicateContentInFolderViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler,
        IDeleteConfirmation deleteConfirmation, IFolderPicker folderPicker, ScanCache? scanCache = null,
        LastFolderStore? lastFolderStore = null) :
        base(trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker, lastFolderStore)
    {
        _scanCache = scanCache;
    }

    protected override Dictionary<HashKey, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct)
    {
        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        using ControlWriter textDisplayProgress = new(this);

        IHashFile fileHashing = HashAlgorithmFactory.Create(HashAlgorithm);
        FileSearchFilter searchFilter = CreateSearchFilter();

        Func<string, byte[]> compute = p => Hash(p, fileHashing, textDisplayProgress, ct);
        Func<string, byte[]> hash = _scanCache is null
            ? compute
            : p => _scanCache.GetOrCompute(HashAlgorithm, p, compute);

        try
        {
            return DupProvider.FindDuplicateByHash(SearchPath, searchOption, searchFilter.MatchesFile,
                textDisplayProgress, UpdateProgress, hash, ct,
                path => searchFilter.MatchesPath(path) && !IsInTrash(path));
        }
        finally
        {
            _scanCache?.Save();
        }
    }

    static byte[] Hash(string path, IHashFile hashFile, TextWriter textDisplayProgress, CancellationToken cancellationToken)
    {
        textDisplayProgress.Write($"Hashing {path}... ");
        byte[] retVal = hashFile.Hash(path, cancellationToken);
        textDisplayProgress.WriteLine(HashFormatter.Format(retVal));
        return retVal;
    }
}