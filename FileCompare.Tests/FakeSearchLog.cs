using DuplicateAssistant.ViewModels;

namespace FileCompare.Tests;

internal sealed class FakeSearchLog : IHaveSearchLog
{
    private string _searchLog = string.Empty;

    public int Updates { get; private set; }

    public string SearchLog
    {
        get => _searchLog;
        set
        {
            _searchLog = value;
            Updates++;
        }
    }
}
