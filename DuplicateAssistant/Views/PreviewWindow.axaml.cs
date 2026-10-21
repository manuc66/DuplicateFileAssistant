using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DuplicateAssistant.Business;
using DuplicateAssistant.ViewModels;

namespace DuplicateAssistant.Views;

public partial class PreviewWindow : Window
{
    private readonly PanState _pan = new();
    private bool _isPanning;

    public PreviewWindow()
    {
        InitializeComponent();
    }

    public PreviewWindow(PreviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Opened += async (_, _) => await viewModel.LoadAsync();
    }

    public void ToggleFullScreen()
    {
        WindowState = WindowState == WindowState.FullScreen
            ? WindowState.Normal
            : WindowState.FullScreen;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is PreviewViewModel viewModel)
        {
            switch (e.Key)
            {
                case Key.Add:
                case Key.OemPlus:
                    viewModel.ZoomIn();
                    e.Handled = true;
                    break;
                case Key.Subtract:
                case Key.OemMinus:
                    viewModel.ZoomOut();
                    e.Handled = true;
                    break;
                case Key.D0:
                case Key.NumPad0:
                    viewModel.ResetZoom();
                    e.Handled = true;
                    break;
                case Key.F11:
                    ToggleFullScreen();
                    e.Handled = true;
                    break;
            }
        }

        base.OnKeyDown(e);
    }

    private void ToggleFullScreen_Click(object? sender, RoutedEventArgs e)
    {
        ToggleFullScreen();
    }

    private void ImageScroll_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ScrollViewer scroll ||
            !e.GetCurrentPoint(scroll).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPanning = true;
        _pan.Begin(e.GetPosition(scroll), scroll.Offset);
        e.Pointer.Capture(scroll);
        e.Handled = true;
    }

    private void ImageScroll_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning || sender is not ScrollViewer scroll)
        {
            return;
        }

        scroll.Offset = _pan.Move(
            e.GetPosition(scroll),
            new Vector(scroll.Extent.Width, scroll.Extent.Height),
            new Vector(scroll.Viewport.Width, scroll.Viewport.Height));
        e.Handled = true;
    }

    private void ImageScroll_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void ImageScroll_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPanning = false;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
