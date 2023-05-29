using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DuplicateAssistant.Views
{
    public partial class DuplicateContentInFolderUserControl : UserControl
    {
        private int _lastSearchLogSize = 0;
        private int _lastCaretIndex = 0;
        private readonly TextBox? _searchLog;

        public DuplicateContentInFolderUserControl()
        {
            InitializeComponent();
            _searchLog = this.FindControl<TextBox>("SearchLog");
            _searchLog!.PropertyChanged += OnSearchLogPropertyChanged;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //https://github.com/AvaloniaUI/Avalonia/issues/6671
                _searchLog.FontFamily = new("Courier New");
            }
        }
        void OnSearchLogPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs eventArgs)
        {
            if (_searchLog == null || eventArgs.Property.Name != nameof(_searchLog.Text))
                return;
            
            // https://github.com/AvaloniaUI/Avalonia/issues/3036
            string searchLogText = _searchLog.Text ?? string.Empty;
            if (_lastSearchLogSize != searchLogText.Length)
            {
                int? lastIndexOf = _searchLog.Text?.LastIndexOf(Environment.NewLine);
                int caretIndex = _searchLog.CaretIndex;
                int newCaret = lastIndexOf.HasValue ? lastIndexOf.Value + 1 : 0;
                if (caretIndex != newCaret)
                {
                    _searchLog.CaretIndex = newCaret;
                }

                _lastSearchLogSize = searchLogText.Length;
                _lastCaretIndex = newCaret;
            }
            else
            {
                int caretIndex = _searchLog.CaretIndex;
                if (caretIndex != _lastCaretIndex)
                {
                    _searchLog.CaretIndex = _lastCaretIndex;
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}