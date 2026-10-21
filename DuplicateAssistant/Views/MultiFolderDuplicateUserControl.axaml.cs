using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DuplicateAssistant.Views;

public partial class MultiFolderDuplicateUserControl : UserControl
{
    public MultiFolderDuplicateUserControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
