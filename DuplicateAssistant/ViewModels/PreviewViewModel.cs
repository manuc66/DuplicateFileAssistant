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

    public PreviewViewModel(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        ImagePath = imagePath;
        Title = Path.GetFileName(imagePath);
        ZoomInCommand = ReactiveCommand.Create(ZoomIn);
        ZoomOutCommand = ReactiveCommand.Create(ZoomOut);
        ResetZoomCommand = ReactiveCommand.Create(ResetZoom);
    }

    public string ImagePath { get; }

    public string Title { get; }

    public Bitmap? Image
    {
        get => _image;
        private set => this.RaiseAndSetIfChanged(ref _image, value);
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

        OnUiThread(() =>
        {
            try
            {
                using MemoryStream stream = new(png);
                Image = new Bitmap(stream);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                Status = exception.Message;
            }
        });
    }
}
