using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DuplicateAssistant.Views;

public partial class InvalidLinkUserControl : UserControl
{
    public InvalidLinkUserControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
