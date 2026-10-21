using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DuplicateAssistant.Views;

public partial class TrashUserControl : UserControl
{
    public TrashUserControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
