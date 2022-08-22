using System.IO;
using System.Text;

namespace DuplicateAssistant;

internal class ControlWriter : TextWriter
{
    private readonly StringBuilder _content = new();
    private readonly IHaveSearchLog _searchLogHolder;

    public ControlWriter(IHaveSearchLog searchLogHolder)
    {
        _searchLogHolder = searchLogHolder;
    }

    public override void Write(char value)
    {
        _content.Append(value);
        _searchLogHolder.SearchLog = _content.ToString();
    }

    public override void Write(string? value)
    {
        _content.Append(value);
        _searchLogHolder.SearchLog = _content.ToString();
    }

    public override Encoding Encoding => Encoding.UTF8;
}