using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using DuplicateAssistant.Views;

namespace DuplicateAssistant.Business;

public class DeleteConfirmationService : IDeleteConfirmation
{
    public async Task<bool> ConfirmAsync(string filePath)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } owner
            })
        {
            return false;
        }

        ConfirmDeleteWindow dialog = new(filePath);
        return await dialog.ShowDialog<bool>(owner);
    }
}
