using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Splat;

namespace DuplicateAssitant;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = Locator.Current.GetService<MainWindowViewModel>();
    }
}