using System;
using System.IO;
using System.Threading;
using HashDepot;

namespace DuplicateAssistant.Business.FileHashing;

public class XXHashFileHashing : IHashFile
{
    public byte[] Hash(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using FileStream fs = File.OpenRead(path);
        using CancellationCheckingStream stream = new(fs, cancellationToken);

        return BitConverter.GetBytes(XXHash.Hash64(stream));
    }

    private sealed class CancellationCheckingStream : Stream
    {
        private readonly Stream _inner;
        private readonly CancellationToken _cancellationToken;

        public CancellationCheckingStream(Stream inner, CancellationToken cancellationToken)
        {
            _inner = inner;
            _cancellationToken = cancellationToken;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            return _inner.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            return _inner.Read(buffer);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
