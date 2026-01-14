using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Dataportal.Services
{
    public class NotebookApiLimitedStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maxBytes;
        private long _bytesWritten;

        public NotebookApiLimitedStream(Stream inner, long maxBytes)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _maxBytes = maxBytes;
        }

        public long BytesWritten => _bytesWritten;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.FlushAsync().GetAwaiter().GetResult();

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureWithinLimit(count);
            _inner.WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
            _bytesWritten += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureWithinLimit(buffer.Length);
            _inner.WriteAsync(buffer.ToArray(), 0, buffer.Length).GetAwaiter().GetResult();
            _bytesWritten += buffer.Length;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureWithinLimit(count);
            await _inner.WriteAsync(buffer, offset, count, cancellationToken);
            _bytesWritten += count;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureWithinLimit(buffer.Length);
            await _inner.WriteAsync(buffer, cancellationToken);
            _bytesWritten += buffer.Length;
        }

        private void EnsureWithinLimit(int bytesToWrite)
        {
            if (_maxBytes <= 0)
            {
                return;
            }

            if (_bytesWritten + bytesToWrite > _maxBytes)
            {
                throw new NotebookApiResponseTooLargeException("Notebook API response exceeded the configured size limit.");
            }
        }
    }
}
