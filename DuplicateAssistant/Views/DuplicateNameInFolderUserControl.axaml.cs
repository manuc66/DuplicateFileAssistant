using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DuplicateAssistant.Views
{
    public partial class DuplicateNameInFolderUserControl : UserControl
    {
        public DuplicateNameInFolderUserControl()
        {
            InitializeComponent();
            TextBox? searchLog = this.FindControl<TextBox>("SearchLog");
            searchLog!.PropertyChanged += (_, _) =>
            {
                // https://github.com/AvaloniaUI/Avalonia/issues/3036
                int? lastIndexOf = searchLog.Text?.LastIndexOf(Environment.NewLine);
                searchLog.CaretIndex = lastIndexOf.HasValue ? lastIndexOf.Value + 1 : 0;
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //https://github.com/AvaloniaUI/Avalonia/issues/6671
                searchLog.FontFamily = new("Courier New");
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}