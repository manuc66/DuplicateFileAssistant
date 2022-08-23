using Avalonia.Controls;
using Splat;

namespace DuplicateAssistant;

public partial class MainWindow : Window
{
   
    public static Window Instance { get; private set; }
    public MainWindow()
    {
        InitializeComponent();
        DataContext = Locator.Current.GetService<MainWindowViewModel>();
        Instance = this;
    }
}