using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DuplicateAssistant
{
    public partial class DuplicateInFolderUserControl : UserControl
    {
        public DuplicateInFolderUserControl()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}