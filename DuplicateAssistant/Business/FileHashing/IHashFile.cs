using System.Threading;

namespace DuplicateAssistant.Business.FileHashing;

public interface IHashFile
{
    byte[] Hash(string path, CancellationToken cancellationToken = default);
}
