using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DuplicateAssistant.Views;

public partial class MergeToDestinationUserControl : UserControl
{
    public MergeToDestinationUserControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
