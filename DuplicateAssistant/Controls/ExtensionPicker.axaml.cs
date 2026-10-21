using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DuplicateAssistant.ViewModels;

namespace DuplicateAssistant.Controls;

public partial class ExtensionPicker : UserControl
{
    public ExtensionPicker()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ExtensionSelectionViewModel viewModel)
        {
            viewModel.IsOpen = false;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
