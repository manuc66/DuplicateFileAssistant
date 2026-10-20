using DuplicateAssistant.Business;

namespace FileCompare.Tests;

internal sealed class FakeFolderPicker : IFolderPicker
{
    public string? NextPath { get; set; }

    public Task<string?> PickFolderAsync(string? currentPath)
    {
        return Task.FromResult(NextPath);
    }
}
