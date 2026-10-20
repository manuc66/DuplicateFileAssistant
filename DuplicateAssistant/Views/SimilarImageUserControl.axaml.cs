using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DuplicateAssistant.ViewModels;

namespace DuplicateAssistant.Views;

public partial class SimilarImageUserControl : UserControl
{
    public SimilarImageUserControl()
    {
        InitializeComponent();
    }

    private void OpenKeeperPreview_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SimilarImageViewModel { DuplicateCase.Keeper: { } keeper })
        {
            OpenPreview(keeper.FilePath);
        }
    }

    private void OpenSelectedPreview_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SimilarImageViewModel { DuplicateCase.SelectedFile: { } selected })
        {
            OpenPreview(selected.FilePath);
        }
    }

    private void OpenPreview(string path)
    {
        PreviewWindow window = new(new PreviewViewModel(path));

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
