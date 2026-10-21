using DuplicateAssistant.Business;

namespace FileCompare.Tests;

internal sealed class FakeDeleteConfirmation : IDeleteConfirmation
{
    public bool Result { get; set; }
    public string? LastPath { get; private set; }

    public Task<bool> ConfirmAsync(string filePath)
    {
        LastPath = filePath;
        return Task.FromResult(Result);
    }
}
