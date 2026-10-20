using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DuplicateAssistant.Views;

public partial class EmptyFolderUserControl : UserControl
{
    public EmptyFolderUserControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
