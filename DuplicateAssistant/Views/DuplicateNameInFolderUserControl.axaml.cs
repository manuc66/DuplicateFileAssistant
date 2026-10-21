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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                TextBox? searchLog = this.FindControl<TextBox>("SearchLog");
                if (searchLog != null)
                {
                    //https://github.com/AvaloniaUI/Avalonia/issues/6671
                    searchLog.FontFamily = new("Courier New");
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
