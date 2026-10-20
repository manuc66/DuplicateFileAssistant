using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    }

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
