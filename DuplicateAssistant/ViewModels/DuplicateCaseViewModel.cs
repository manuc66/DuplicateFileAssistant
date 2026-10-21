using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using DuplicateAssistant.Business;
using FileCompare;
using ImageCompare;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class DuplicateCaseViewModel : ViewModelBase
{
    private readonly DuplicateInFolderViewModel _duplicateInFolderViewModel;
    private string _name;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private ObservableCollection<DuplicateCaseItemViewModel> _files;

    public ObservableCollection<DuplicateCaseItemViewModel> Files
    {
        get => _files;
        set => this.RaiseAndSetIfChanged(ref _files, value);
    }

    private DuplicateCaseItemViewModel? _keeper;

    public DuplicateCaseItemViewModel? Keeper
    {
        get => _keeper;
        private set => this.RaiseAndSetIfChanged(ref _keeper, value);
    }

    public SimilarImageKind? Kind { get; private set; }

    public string KindText { get; private set; } = string.Empty;

    public string Detail { get; private set; } = string.Empty;

    public string SimilarityText { get; private set; } = string.Empty;

    public string KeeperQualityText { get; private set; } = string.Empty;

    private Bitmap? _thumbnail;

    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        private set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
    }

    private bool _previewsEnabled;
    private DuplicateCaseItemViewModel? _selectedFile;
    private Bitmap? _keeperPreview;
    private Bitmap? _selectedPreview;

    public DuplicateCaseItemViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (ReferenceEquals(_selectedFile, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedFile, value);

            if (_previewsEnabled)
            {
                _ = LoadSelectedPreviewAsync(value);
            }
        }
    }

    public Bitmap? KeeperPreview
    {
        get => _keeperPreview;
        private set => this.RaiseAndSetIfChanged(ref _keeperPreview, value);
    }

    public Bitmap? SelectedPreview
    {
        get => _selectedPreview;
        private set => this.RaiseAndSetIfChanged(ref _selectedPreview, value);
    }

    public long ReclaimableSize
    {
        get
        {
            return Files
                .Where(file => !ReferenceEquals(file, Keeper) && !file.IsDeleted)
                .Sum(file => file.FileSize);
        }
    }

    public string ReclaimableSizeText => $"Reclaimable: {FileSizeFormatter.Format(ReclaimableSize)}";

    public string KeeperText => Keeper is null ? "Keep: none" : $"Keep: {Keeper.FilePath}";

    public DuplicateCaseViewModel(DuplicateInFolderViewModel duplicateInFolderViewModel,
        IEnumerable<DuplicateFile> files, Trash trash, FileManagerHandler fileManagerHandler,
        IDeleteConfirmation deleteConfirmation)
    {
        _duplicateInFolderViewModel = duplicateInFolderViewModel;
        _name = String.Join(" - ", files.Select(file => Path.GetFileName(file.Path)).Distinct(StringComparer.OrdinalIgnoreCase));
        _files = new ObservableCollection<DuplicateCaseItemViewModel>(files.Select(file =>
            new DuplicateCaseItemViewModel(this, file, fileManagerHandler, trash, deleteConfirmation)));

        foreach (DuplicateCaseItemViewModel file in _files)
        {
            file.PropertyChanged += OnFilePropertyChanged;
        }

        SetKeeper(_files.FirstOrDefault());
    }

    public void SetKeeper(DuplicateCaseItemViewModel? keeper)
    {
        if (keeper is { IsDeleted: true })
        {
            return;
        }

        foreach (DuplicateCaseItemViewModel file in Files)
        {
            file.ApplyKeeper(ReferenceEquals(file, keeper));
        }

        Keeper = keeper;
        this.RaisePropertyChanged(nameof(KeeperText));
        RaiseReclaimableSizeChanged();

        if (_previewsEnabled)
        {
            _ = LoadKeeperPreviewAsync(keeper);
        }
    }

    public void ApplySimilarImageDetails(SimilarImageKind kind, int maximumDistance,
        double? minimumSimilarity, string keeperPath, double? keeperQuality)
    {
        Kind = kind;
        KindText = kind == SimilarImageKind.Semantic ? "Semantic" : "Perceptual";
        Detail = minimumSimilarity is double similarity
            ? $"{KindText} | min similarity {similarity:F3} | dHash max {maximumDistance}"
            : $"{KindText} | dHash max {maximumDistance}";
        SimilarityText = minimumSimilarity is double minimum
            ? $"Minimum similarity: {minimum:F3}"
            : string.Empty;
        KeeperQualityText = keeperQuality is double quality
            ? $"Keeper quality: {quality:F3}"
            : string.Empty;

        // A perceptual match is not guaranteed to be byte-identical, so a hard
        // link would silently replace a different picture with another one.
        foreach (DuplicateCaseItemViewModel file in Files)
        {
            file.DisableHardlink();
        }

        DuplicateCaseItemViewModel? suggestedKeeper = Files.FirstOrDefault(file =>
            PathUtilities.PathComparer.Equals(file.FilePath, keeperPath));

        if (suggestedKeeper is not null)
        {
            SetKeeper(suggestedKeeper);
        }

        _previewsEnabled = true;
        _selectedFile = Files.FirstOrDefault(file => !file.IsKeeper) ?? Keeper;
        this.RaisePropertyChanged(nameof(SelectedFile));

        this.RaisePropertyChanged(nameof(Kind));
        this.RaisePropertyChanged(nameof(KindText));
        this.RaisePropertyChanged(nameof(Detail));
        this.RaisePropertyChanged(nameof(SimilarityText));
        this.RaisePropertyChanged(nameof(KeeperQualityText));
    }

    public async Task LoadThumbnailAsync(string path)
    {
        byte[]? png = await Task.Run(() => ThumbnailLoader.LoadPng(path, 192, 144));

        if (png is null)
        {
            return;
        }

        OnUiThread(() =>
        {
            try
            {
                using MemoryStream stream = new(png);
                Thumbnail = new Bitmap(stream);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
            }
        });
    }

    public async Task LoadPreviewsAsync()
    {
        if (!_previewsEnabled)
        {
            return;
        }

        await LoadKeeperPreviewAsync(Keeper);
        await LoadSelectedPreviewAsync(SelectedFile ?? Keeper);
    }

    private async Task LoadKeeperPreviewAsync(DuplicateCaseItemViewModel? keeper)
    {
        if (!_previewsEnabled || keeper is null)
        {
            return;
        }

        byte[]? png = await Task.Run(() => ThumbnailLoader.LoadPng(keeper.FilePath, 640, 480));

        if (png is null)
        {
            return;
        }

        OnUiThread(() =>
        {
            try
            {
                using MemoryStream stream = new(png);
                KeeperPreview = new Bitmap(stream);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
            }
        });
    }

    private async Task LoadSelectedPreviewAsync(DuplicateCaseItemViewModel? selected)
    {
        if (!_previewsEnabled || selected is null)
        {
            return;
        }

        byte[]? png = await Task.Run(() => ThumbnailLoader.LoadPng(selected.FilePath, 640, 480));

        if (png is null)
        {
            return;
        }

        OnUiThread(() =>
        {
            try
            {
                using MemoryStream stream = new(png);
                SelectedPreview = new Bitmap(stream);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
            }
        });
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DuplicateCaseItemViewModel.IsDeleted) &&
            sender is DuplicateCaseItemViewModel { IsDeleted: true } file &&
            ReferenceEquals(file, Keeper))
        {
            SetKeeper(Files.FirstOrDefault(candidate => !candidate.IsDeleted));
        }

        RaiseReclaimableSizeChanged();
    }

    private void RaiseReclaimableSizeChanged()
    {
        this.RaisePropertyChanged(nameof(ReclaimableSize));
        this.RaisePropertyChanged(nameof(ReclaimableSizeText));
    }

    public void Remove(DuplicateCaseItemViewModel duplicateCaseItem)
    {
        Files.Remove(duplicateCaseItem);
        if (Files.Count <= 1)
        {
            _duplicateInFolderViewModel.Remove(this);
        }
    }
}