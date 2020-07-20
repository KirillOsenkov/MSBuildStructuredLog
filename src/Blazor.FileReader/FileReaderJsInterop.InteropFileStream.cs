using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace Blazor.FileReader
{
    public partial class FileReaderJsInterop
    {
        private class InteropFileStream : AsyncDisposableStream
        {
            private readonly int fileRef;
            private readonly long length;
            private readonly FileReaderJsInterop fileReaderJsInterop;
            private bool isDisposed;
            private long _position;

            public InteropFileStream(int fileReference, long length, FileReaderJsInterop fileReaderJsInterop)
            {
                this.fileRef = fileReference;
                this.length = length;
                this.fileReaderJsInterop = fileReaderJsInterop;
            }

            public override bool CanRead => ThrowIfDisposedOrReturn(true);

            public override bool CanSeek => ThrowIfDisposedOrReturn(true);

            public override bool CanWrite => ThrowIfDisposedOrReturn(false);

            public override long Length => ThrowIfDisposedOrReturn(length);

            public override long Position {
                get => ThrowIfDisposedOrReturn(_position);
                set {
                    ThrowIfDisposed();
                    _position = value;
                }
            }

            public override void Flush()
            {
                ThrowIfDisposed();
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                ThrowIfDisposed();
                var bytesRead = await fileReaderJsInterop.ReadFileAsync(fileRef, buffer, Position, offset, count, cancellationToken);
                Position += bytesRead;
                return bytesRead;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException("Synchronous read is not supported by this stream. Use ReadAsync().");
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                ThrowIfDisposed();
                if (offset > Length)
                    throw new ArgumentOutOfRangeException("offset");
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        {
                            if (offset < 0)
                                throw new IOException("SeekBeforeBegin");
                            Position = offset;
                            break;
                        }
                    case SeekOrigin.Current:
                        {
                            int tempPosition = unchecked((int)Position + (int)offset);
                            if (unchecked((int)Position + offset) < 0 || tempPosition < 0)
                                throw new IOException("SeekBeforeBegin");
                            Position = tempPosition;
                            break;
                        }
                    case SeekOrigin.End:
                        {
                            int tempPosition = unchecked((int)(Length + (int)offset));
                            if (unchecked(Length + offset) < 0 || tempPosition < 0)
                                throw new IOException("IO.IO_SeekBeforeBegin");
                            Position = tempPosition;
                            break;
                        }
                    default:
                        throw new ArgumentException("Argument_InvalidSeekOrigin");
                }

                return Position;
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
                base.Dispose(disposing);
                if (!isDisposed)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    // Fire-and-forget dispose as the only impact is the js GC
                    this.fileReaderJsInterop.DisposeStream(fileRef);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    isDisposed = true;
                }
            }

            private void ThrowIfDisposed()
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException(nameof(InteropFileStream));
                }
            }

            private T ThrowIfDisposedOrReturn<T>(T value)
            {
                ThrowIfDisposed();

                return value;
            }

            public override async ValueTask DisposeAsync()
            {
                if (!isDisposed)
                {
                    await this.fileReaderJsInterop.DisposeStream(fileRef);
                    isDisposed = true;
                }
            }
        }
    }
}
