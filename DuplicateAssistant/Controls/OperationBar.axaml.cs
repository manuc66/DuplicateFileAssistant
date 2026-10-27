using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DuplicateAssistant.Controls;

public partial class OperationBar : UserControl
{
    public OperationBar()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
