using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DuplicateAssistant.Controls;

namespace FileCompare.Tests;

[TestFixture]
public class AutoScrollTextBoxTests
{
    [AvaloniaTest]
    public void TextChangesScrollToTheEnd()
    {
        AutoScrollTextBox textBox = new()
        {
            Width = 300,
            Height = 100
        };

        Window window = new()
        {
            Width = 320,
            Height = 120,
            Content = textBox
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        textBox.Text = string.Join(Environment.NewLine, Enumerable.Range(0, 200).Select(index => $"line {index}"));

        Dispatcher.UIThread.RunJobs();

        ScrollViewer? scrollViewer = textBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

        Assert.Multiple(() =>
        {
            Assert.That(textBox.CaretIndex, Is.EqualTo(textBox.Text!.Length));
            Assert.That(scrollViewer, Is.Not.Null);
            Assert.That(scrollViewer!.Offset.Y, Is.GreaterThan(0));
        });

        window.Close();
    }
}
