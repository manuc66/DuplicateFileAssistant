using System.Threading.Tasks;

namespace DuplicateAssistant.Business;

public interface IDeleteConfirmation
{
    Task<bool> ConfirmAsync(string filePath);
}
