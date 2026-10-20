using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DuplicateAssistant.Views;

public partial class ConfirmDeleteWindow : Window
{
    public ConfirmDeleteWindow()
    {
        InitializeComponent();
    }

    public ConfirmDeleteWindow(string filePath) : this()
    {
        MessageText.Text = $"Move this file to the trash?\n\n{filePath}";
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
