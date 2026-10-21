using System;
using System.Buffers;

namespace DuplicateAssistant.Business;

public sealed class BoundedTextBuffer : IDisposable
{
    private readonly char[] _buffer;
    private int _start;
    private int _length;
    private bool _disposed;

    public BoundedTextBuffer(int maxLength)
    {
        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        MaxLength = maxLength;
        _buffer = ArrayPool<char>.Shared.Rent(maxLength);
    }

    public int MaxLength { get; }
    public int Length => _length;

    public void Append(char value)
    {
        if (_length == MaxLength)
        {
            _buffer[_start] = value;
            _start = (_start + 1) % MaxLength;
            return;
        }

        _buffer[(_start + _length) % MaxLength] = value;
        _length++;
    }

    public void Append(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Append(value.AsSpan());
        }
    }

    public void Append(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        if (value.Length >= MaxLength)
        {
            value[^MaxLength..].CopyTo(_buffer);
            _start = 0;
            _length = MaxLength;
            return;
        }

        int overflow = _length + value.Length - MaxLength;

        if (overflow > 0)
        {
            _start = (_start + overflow) % MaxLength;
            _length -= overflow;
        }

        int writeIndex = (_start + _length) % MaxLength;
        int firstPartLength = Math.Min(value.Length, MaxLength - writeIndex);
        value[..firstPartLength].CopyTo(_buffer.AsSpan(writeIndex, firstPartLength));

        if (firstPartLength < value.Length)
        {
            value[firstPartLength..].CopyTo(_buffer);
        }

        _length += value.Length;
    }

    public void AppendLine(string? value)
    {
        Append(value);
        Append(Environment.NewLine);
    }

    public override string ToString()
    {
        if (_length == 0)
        {
            return string.Empty;
        }

        if (_start + _length <= MaxLength)
        {
            return new string(_buffer, _start, _length);
        }

        int firstPartLength = MaxLength - _start;
        return string.Concat(
            _buffer.AsSpan(_start, firstPartLength),
            _buffer.AsSpan(0, _length - firstPartLength));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ArrayPool<char>.Shared.Return(_buffer);
    }
}
