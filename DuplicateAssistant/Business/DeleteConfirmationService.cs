using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using DuplicateAssistant.Views;

namespace DuplicateAssistant.Business;

public class DeleteConfirmationService : IDeleteConfirmation
{
    public Task<bool> ConfirmAsync(string filePath)
    {
        return ConfirmAsync(filePath, "Move this file to the trash?");
    }

    public async Task<bool> ConfirmAsync(string path, string message)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } owner
            })
        {
            return false;
        }

        ConfirmDeleteWindow dialog = new(path, message);
        return await dialog.ShowDialog<bool>(owner);
    }
}
