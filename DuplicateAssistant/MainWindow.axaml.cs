using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Splat;

namespace DuplicateAssistant;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = Locator.Current.GetService<MainWindowViewModel>();
    }
}