using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using DuplicateAssistant.Business;
using FileCompare;
using ImageCompare;
using ImageCompare.Onnx;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class SimilarImageViewModel : DuplicateInFolderViewModel
{
    private readonly ScanCache? _scanCache;
    private readonly Dictionary<HashKey, SimilarGroupMetadata> _metadata = new();

    private bool _useSemantic;
    private bool _useKeeper = true;
    private bool _useKeeperModel;
    private bool _allowModelDownload = true;
    private decimal _semanticThreshold = 0.80m;
    private string _summary = string.Empty;
    private string _cacheSummary = "Click Refresh to inspect the caches.";

    public bool LoadThumbnails { get; set; } = true;

    public SimilarImageViewModel(Trash trash, string searchPath, FileManagerHandler fileManagerHandler,
        IDeleteConfirmation deleteConfirmation, IFolderPicker folderPicker, ScanCache? scanCache = null) :
        base(trash, searchPath, fileManagerHandler, deleteConfirmation, folderPicker)
    {
        _scanCache = scanCache;
        ShowHashOptions = false;
        DryRun = true;
        FileExtensions = "jpg,jpeg,png,gif,bmp,webp,ico";
        MinimumFileSizeText = "10240";
        RefreshCacheSummaryCommand = ReactiveCommand.CreateFromTask(RefreshCacheSummaryAsync);
        PruneCachesCommand = ReactiveCommand.CreateFromTask(PruneCachesAsync);
        ClearCachesCommand = ReactiveCommand.CreateFromTask(ClearCachesAsync);
        OpenModelFolderCommand = ReactiveCommand.Create(OpenModelFolder);
    }

    public string ModelDirectory => CachePaths.ModelsDirectory;

    public string CacheDirectory => CachePaths.RootDirectory;

    public string CacheSummary
    {
        get => _cacheSummary;
        private set => this.RaiseAndSetIfChanged(ref _cacheSummary, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCacheSummaryCommand { get; }

    public ReactiveCommand<Unit, Unit> PruneCachesCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearCachesCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenModelFolderCommand { get; }

    public bool UseSemantic
    {
        get => _useSemantic;
        set
        {
            this.RaiseAndSetIfChanged(ref _useSemantic, value);
            this.RaisePropertyChanged(nameof(NeedsModelDownload));
        }
    }

    public bool UseKeeper
    {
        get => _useKeeper;
        set
        {
            this.RaiseAndSetIfChanged(ref _useKeeper, value);
            this.RaisePropertyChanged(nameof(NeedsModelDownload));
        }
    }

    public bool UseKeeperModel
    {
        get => _useKeeperModel;
        set
        {
            this.RaiseAndSetIfChanged(ref _useKeeperModel, value);
            this.RaisePropertyChanged(nameof(NeedsModelDownload));
        }
    }

    public bool AllowModelDownload
    {
        get => _allowModelDownload;
        set => this.RaiseAndSetIfChanged(ref _allowModelDownload, value);
    }

    public decimal SemanticThreshold
    {
        get => _semanticThreshold;
        set => this.RaiseAndSetIfChanged(ref _semanticThreshold, value);
    }

    public bool NeedsModelDownload => UseSemantic || UseKeeper && UseKeeperModel;

    public string Summary
    {
        get => _summary;
        private set => this.RaiseAndSetIfChanged(ref _summary, value);
    }

    protected override Dictionary<HashKey, HashSet<DuplicateFile>> SearchFunction(CancellationToken ct)
    {
        using ControlWriter log = new(this);
        OnUiThread(() => Summary = string.Empty);

        SearchOption searchOption = SubFolder ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        FileSearchFilter filter = CreateSearchFilter();

        IImageEmbeddingProvider? embeddings = null;
        IQualityScorer? quality = null;
        EmbeddingCache? embeddingCache = null;
        QualityCache? qualityCache = null;

        try
        {
            OnnxModelStore? store = NeedsModelDownload ? new OnnxModelStore() : null;

            if (UseSemantic)
            {
                log.WriteLine("Loading DINOv2-small...");
                embeddings = OnnxProviders.CreateImageEmbeddingAsync(
                        "dinov2-small",
                        store!,
                        AllowModelDownload,
                        message => log.WriteLine(message),
                        ct)
                    .GetAwaiter()
                    .GetResult();

                embeddingCache = new EmbeddingCache(
                    EmbeddingCache.DefaultPath(embeddings.Id),
                    embeddings.Dimensions);
            }

            if (UseKeeper)
            {
                if (UseKeeperModel)
                {
                    log.WriteLine("Loading DBCNN...");
                    quality = OnnxProviders.CreateQualityScorerAsync(
                            "dbcnn",
                            store!,
                            AllowModelDownload,
                            message => log.WriteLine(message),
                            ct)
                        .GetAwaiter()
                        .GetResult();
                }
                else
                {
                    quality = new HeuristicQualityScorer();
                }

                qualityCache = new QualityCache(QualityCache.DefaultPath(quality.Id), quality.Id);
                quality = new CachedQualityScorer(quality, qualityCache);
            }

            SimilarImageOptions options = new()
            {
                MaxDistance = SimilarImageFinder.DefaultMaxDistance,
                Embeddings = embeddings,
                SemanticThreshold = (double)SemanticThreshold,
                Quality = quality
            };

            SimilarImageScanResult result = SimilarImageFinder.Find(
                SearchPath,
                searchOption,
                filter,
                options,
                log,
                UpdateProgress,
                ct,
                path => !IsInTrash(path),
                cache: _scanCache,
                embeddingCache: embeddingCache);

            string summary = result.Groups.Count == 0
                ? $"No similar images found ({result.HashedCount} scanned)."
                : $"{result.Groups.Count} group(s), {result.RedundantCount} redundant image(s), " +
                  $"{result.HashedCount} scanned.";

            OnUiThread(() => Summary = summary);
            log.WriteLine(summary);

            return ToGroups(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            OnUiThread(() => Summary = exception.Message);
            throw;
        }
        finally
        {
            TrySave(_scanCache);
            TrySave(embeddingCache);
            TrySave(qualityCache);
            embeddings?.Dispose();
            quality?.Dispose();
        }
    }

    protected override DuplicateCaseViewModel CreateDuplicateCase(HashKey key, IEnumerable<DuplicateFile> files)
    {
        DuplicateCaseViewModel duplicateCase = base.CreateDuplicateCase(key, files);

        if (_metadata.TryGetValue(key, out SimilarGroupMetadata? metadata))
        {
            duplicateCase.ApplySimilarImageDetails(
                metadata.Kind,
                metadata.MaximumDistance,
                metadata.MinimumSimilarity,
                metadata.KeeperPath,
                metadata.KeeperQuality);

            if (LoadThumbnails)
            {
                _ = duplicateCase.LoadThumbnailAsync(metadata.KeeperPath);
            }
        }

        return duplicateCase;
    }

    private Dictionary<HashKey, HashSet<DuplicateFile>> ToGroups(SimilarImageScanResult result)
    {
        Dictionary<HashKey, HashSet<DuplicateFile>> groups = new();
        _metadata.Clear();
        int index = 0;

        foreach (SimilarImageGroup group in result.Groups)
        {
            HashKey key = new(BitConverter.GetBytes(index++));
            HashSet<DuplicateFile> files = new(DuplicateFile.Comparer);

            foreach (SimilarImageMatch match in group.Images)
            {
                DateTime lastWriteTimeUtc = DateTime.UtcNow;

                try
                {
                    lastWriteTimeUtc = File.GetLastWriteTimeUtc(match.Image.Path);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                }

                files.Add(new DuplicateFile(match.Image.Path, match.Image.Length, lastWriteTimeUtc));
            }

            groups[key] = files;
            _metadata[key] = new SimilarGroupMetadata(
                group.Kind,
                group.MaximumDistance,
                group.MinimumSimilarity,
                group.Keeper.Path,
                group.KeeperQuality);
        }

        return groups;
    }

    private async Task RefreshCacheSummaryAsync()
    {
        try
        {
            CacheSnapshot snapshot = await Task.Run(() =>
                CacheInspector.Inspect(CachePaths.RootDirectory, CachePaths.ModelsDirectory));

            OnUiThread(() => CacheSummary = FormatCacheSummary(snapshot));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            OnUiThread(() => CacheSummary = exception.Message);
        }
    }

    private async Task PruneCachesAsync()
    {
        try
        {
            int removed = await Task.Run(() => CacheInspector.Prune(CachePaths.RootDirectory));
            await RefreshCacheSummaryAsync();

            OnUiThread(() => Summary =
                $"Pruned {removed} cache entr{(removed == 1 ? "y" : "ies")} for missing files.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            OnUiThread(() => Summary = exception.Message);
        }
    }

    private async Task ClearCachesAsync()
    {
        bool confirmed = await DeleteConfirmation.ConfirmAsync(
            CachePaths.RootDirectory,
            "Delete cached hashes, embeddings and quality scores? Downloaded models are kept.");

        if (!confirmed)
        {
            return;
        }

        try
        {
            CacheCleanupResult cleanup = await Task.Run(() => CacheInspector.Clear(CachePaths.RootDirectory));
            await RefreshCacheSummaryAsync();

            OnUiThread(() => Summary =
                $"Cleared {cleanup.Files} cache file(s), {FormatBytes(cleanup.Bytes)}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            OnUiThread(() => Summary = exception.Message);
        }
    }

    private void OpenModelFolder()
    {
        try
        {
            Directory.CreateDirectory(CachePaths.ModelsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = CachePaths.ModelsDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidOperationException or Win32Exception)
        {
            OnUiThread(() => Summary = exception.Message);
        }
    }

    private static string FormatCacheSummary(CacheSnapshot snapshot)
    {
        if (snapshot.Entries.Count == 0)
        {
            return "Caches are empty.";
        }

        string[] parts = snapshot.Entries
            .GroupBy(entry => entry.Kind)
            .Select(group => $"{group.Key} {FormatBytes(group.Sum(entry => entry.Bytes))}")
            .ToArray();

        return $"{FormatBytes(snapshot.TotalBytes)} total | {string.Join(", ", parts)}";
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:F1} MB",
            _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB"
        };
    }

    private static void TrySave(ScanCache? cache)
    {
        try
        {
            cache?.Save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TrySave(EmbeddingCache? cache)
    {
        try
        {
            cache?.Save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TrySave(QualityCache? cache)
    {
        try
        {
            cache?.Save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record SimilarGroupMetadata(
        SimilarImageKind Kind,
        int MaximumDistance,
        double? MinimumSimilarity,
        string KeeperPath,
        double? KeeperQuality);
}
