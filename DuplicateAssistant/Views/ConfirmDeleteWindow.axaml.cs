using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DuplicateAssistant.Views;

public partial class ConfirmDeleteWindow : Window
{
    public ConfirmDeleteWindow()
    {
        InitializeComponent();
    }

    public ConfirmDeleteWindow(string filePath) : this(filePath, "Move this file to the trash?")
    {
    }

    public ConfirmDeleteWindow(string path, string message) : this()
    {
        MessageText.Text = $"{message}\n\n{path}";
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
