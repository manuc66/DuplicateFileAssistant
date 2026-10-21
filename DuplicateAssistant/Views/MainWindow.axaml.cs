using Avalonia.Controls;
using DuplicateAssistant.ViewModels;

namespace DuplicateAssistant.Views;

public partial class MainWindow : Window
{
    private readonly ListBox[] _navigationLists;
    private bool _syncingSelection;

    public MainWindow()
    {
        InitializeComponent();

        _navigationLists =
        [
            DuplicateNavigation,
            OrganizeNavigation,
            MaintenanceNavigation,
            UtilityNavigation
        ];

        DuplicateNavigation.SelectedIndex = 0;
        Pages.SelectedIndex = 0;
    }

    private void OnNavigationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || sender is not ListBox list)
        {
            return;
        }

        if (list.SelectedItem is not ListBoxItem { Tag: string tag } || !int.TryParse(tag, out int pageIndex))
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            foreach (ListBox other in _navigationLists)
            {
                if (!ReferenceEquals(other, list))
                {
                    other.SelectedItem = null;
                }
            }

            Pages.SelectedIndex = pageIndex;
        }
        finally
        {
            _syncingSelection = false;
        }
    }
}
