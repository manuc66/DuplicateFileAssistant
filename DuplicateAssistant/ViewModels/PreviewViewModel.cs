using System;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using DuplicateAssistant.Business;
using ReactiveUI.Reactive;

namespace DuplicateAssistant.ViewModels;

public class PreviewViewModel : ViewModelBase
{
    public const double MinimumZoom = 0.1;
    public const double MaximumZoom = 8.0;

    private const double ZoomStep = 1.25;

    private Bitmap? _image;
    private double _zoom = 1.0;
    private Bitmap? _comparisonImage;
    private Bitmap? _differenceImage;
    private string _differenceText = string.Empty;
    private double _blend;
    private bool _showDifference;

    public PreviewViewModel(string imagePath, string? comparisonPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        ImagePath = imagePath;
        ComparisonPath = comparisonPath;
        Title = Path.GetFileName(imagePath);
        ZoomInCommand = ReactiveCommand.Create(ZoomIn);
        ZoomOutCommand = ReactiveCommand.Create(ZoomOut);
        ResetZoomCommand = ReactiveCommand.Create(ResetZoom);
    }

    public string ImagePath { get; }

    public string? ComparisonPath { get; }

    public bool HasComparison => ComparisonPath is not null;

    public string Title { get; }

    public Bitmap? Image
    {
        get => _image;
        private set => this.RaiseAndSetIfChanged(ref _image, value);
    }

    public Bitmap? ComparisonImage
    {
        get => _comparisonImage;
        private set => this.RaiseAndSetIfChanged(ref _comparisonImage, value);
    }

    public Bitmap? DifferenceImage
    {
        get => _differenceImage;
        private set => this.RaiseAndSetIfChanged(ref _differenceImage, value);
    }

    public string DifferenceText
    {
        get => _differenceText;
        private set => this.RaiseAndSetIfChanged(ref _differenceText, value);
    }

    public double Blend
    {
        get => _blend;
        set => this.RaiseAndSetIfChanged(ref _blend, value);
    }

    public bool ShowDifference
    {
        get => _showDifference;
        set => this.RaiseAndSetIfChanged(ref _showDifference, value);
    }

    public double Zoom
    {
        get => _zoom;
        private set
        {
            this.RaiseAndSetIfChanged(ref _zoom, value);
            this.RaisePropertyChanged(nameof(ZoomText));
        }
    }

    public string ZoomText => $"{Zoom:P0}";

    public ReactiveCommand<Unit, Unit> ZoomInCommand { get; }

    public ReactiveCommand<Unit, Unit> ZoomOutCommand { get; }

    public ReactiveCommand<Unit, Unit> ResetZoomCommand { get; }

    public void ZoomIn()
    {
        Zoom = Math.Min(MaximumZoom, Zoom * ZoomStep);
    }

    public void ZoomOut()
    {
        Zoom = Math.Max(MinimumZoom, Zoom / ZoomStep);
    }

    public void ResetZoom()
    {
        Zoom = 1.0;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        byte[]? png = await Task.Run(
            () => ThumbnailLoader.LoadPng(ImagePath, 2048, 2048),
            cancellationToken);

        if (png is null)
        {
            OnUiThread(() => Status = $"Unable to load {ImagePath}.");
            return;
        }

        OnUiThread(() => Image = CreateBitmap(png));

        if (ComparisonPath is null)
        {
            return;
        }

        byte[]? comparisonPng = await Task.Run(
            () => ThumbnailLoader.LoadPng(ComparisonPath, 2048, 2048),
            cancellationToken);

        if (comparisonPng is not null)
        {
            OnUiThread(() => ComparisonImage = CreateBitmap(comparisonPng));
        }

        ImageDifference.Result? difference = await Task.Run(
            () => ImageDifference.Create(ImagePath, ComparisonPath),
            cancellationToken);

        if (difference is null)
        {
            return;
        }

        OnUiThread(() =>
        {
            DifferenceImage = CreateBitmap(difference.Png);
            DifferenceText = $"Normalized difference: {difference.MeanDifference:F1} / 255";
        });
    }

    private Bitmap? CreateBitmap(byte[]? png)
    {
        if (png is null)
        {
            return null;
        }

        try
        {
            using MemoryStream stream = new(png);
            return new Bitmap(stream);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            Status = exception.Message;
            return null;
        }
    }
}
