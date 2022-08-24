using Avalonia.Controls;
using DuplicateAssistant.ViewModels;
using Splat;

namespace DuplicateAssistant.Views;

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