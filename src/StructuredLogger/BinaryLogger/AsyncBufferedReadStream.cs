using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace System.IO
{
    public class AsyncBufferedReadStream : Stream
    {
        private Stream stream;

        private byte[] buffer;
        private readonly int bufferSize;
        private const int DefaultBufferSize = 4096;

        private int readPosition;
        private int readLength;

        private byte[] prefetchBuffer;
        private Task prefetchTask = null;
        private int prefetchReadbytes = 0;

        public AsyncBufferedReadStream(Stream stream)
            : this(stream, DefaultBufferSize)
        {
        }

        public AsyncBufferedReadStream(Stream stream, int bufferSize)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException("bufferSize");
            }

            this.stream = stream;
            this.bufferSize = bufferSize;

            buffer = new byte[this.bufferSize];
            prefetchBuffer = new byte[this.bufferSize];
        }

        private int ReadFromBuffer(byte[] array, int offset, int count)
        {
            int readBytes = readLength - readPosition;
            Contract.Assert(readBytes >= 0);

            if (readBytes == 0)
            {
                return 0;
            }

            Contract.Assert(readBytes > 0);

            if (readBytes > count)
            {
                readBytes = count;
            }

            Buffer.BlockCopy(buffer, readPosition, array, offset, readBytes);
            readPosition += readBytes;

            return readBytes;
        }

        public override int Read([In, Out] byte[] array, int offset, int count)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            if (array.Length - offset < count)
            {
                throw new ArgumentException();
            }

            int bytesFromBuffer = ReadFromBuffer(array, offset, count);

            // We may have read less than the number of bytes the user asked for, but that is part of the Stream contract.

            // Reading again for more data may cause us to block if we're using a device with no clear end of file,
            // such as a serial port or pipe. If we blocked here and this code was used with redirected pipes for a
            // process's standard output, this can lead to deadlocks involving two processes.              
            // BUT - this is a breaking change. 
            // So: If we could not read all bytes the user asked for from the buffer, we will try once from the underlying
            // stream thus ensuring the same blocking behaviour as if the underlying stream was not wrapped in this BufferedStream.
            if (bytesFromBuffer == count)
            {
                return bytesFromBuffer;
            }

            int alreadySatisfied = bytesFromBuffer;
            if (bytesFromBuffer > 0)
            {
                count -= bytesFromBuffer;
                offset += bytesFromBuffer;
            }

            // So the READ buffer is empty.
            Contract.Assert(readLength == readPosition);
            readPosition = readLength = 0;

            // If the requested read is larger than buffer size, avoid the buffer and still use a single read:
            if (count >= bufferSize)
            {
                return stream.Read(array, offset, count) + alreadySatisfied;
            }

            readLength = stream.Read(buffer, 0, bufferSize);

            bytesFromBuffer = ReadFromBuffer(array, offset, count);

            // We may have read less than the number of bytes the user asked for, but that is part of the Stream contract.
            // Reading again for more data may cause us to block if we're using a device with no clear end of stream,
            // such as a serial port or pipe.  If we blocked here & this code was used with redirected pipes for a process's
            // standard output, this can lead to deadlocks involving two processes. Additionally, translating one read on the
            // BufferedStream to more than one read on the underlying Stream may defeat the whole purpose of buffering of the
            // underlying reads are significantly more expensive.

            return bytesFromBuffer + alreadySatisfied;
        }

        public override int ReadByte()
        {
            if (readPosition == readLength)
            {
                RefillBuffer();
                if (readLength == 0)
                {
                    return -1;
                }

                readPosition = 0;
            }

            int b = buffer[readPosition++];
            return b;
        }

        private void RefillBuffer()
        {
            if (prefetchTask == null)
            {
                PrefetchNextBuffer();
            }

            prefetchTask.Wait();

            (buffer, prefetchBuffer) = (prefetchBuffer, buffer);

            readLength = prefetchReadbytes;

            if (readLength != 0)
            {
                PrefetchNextBuffer();
            }
        }

        private void PrefetchNextBuffer()
        {
            prefetchTask = Task.Run(() =>
            {
                prefetchReadbytes = stream.Read(prefetchBuffer, 0, bufferSize);
            });
        }

        public override bool CanRead => stream != null && stream.CanRead;

        public override bool CanWrite => stream != null && stream.CanWrite;

        public override bool CanSeek => stream != null && stream.CanSeek;

        public override long Length => stream.Length;

        public override long Position
        {
            get
            {
                return stream.Position + readPosition - readLength;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && stream != null)
                {
                    try
                    {
                        Flush();
                    }
                    finally
                    {
                        stream.Close();
                    }
                }
            }
            finally
            {
                stream = null;
                buffer = null;
                prefetchBuffer = null;

                base.Dispose(disposing);
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}