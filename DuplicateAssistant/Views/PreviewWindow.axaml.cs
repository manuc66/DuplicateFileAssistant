using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DuplicateAssistant.ViewModels;

namespace DuplicateAssistant.Views;

public partial class PreviewWindow : Window
{
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

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
