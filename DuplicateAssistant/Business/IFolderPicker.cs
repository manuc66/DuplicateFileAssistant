using System.Threading.Tasks;

namespace DuplicateAssistant.Business;

public interface IFolderPicker
{
    Task<string?> PickFolderAsync(string? currentPath);
}
