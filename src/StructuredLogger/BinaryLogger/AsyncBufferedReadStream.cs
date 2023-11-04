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

        public override int Read(byte[] array, int offset, int count)
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

            if (count == 0)
            {
                return 0;
            }

            if (array.Length - offset < count)
            {
                throw new ArgumentException();
            }

            int totalRead = 0;

            while (true)
            {
                int read = ReadFromBuffer(array, offset, count);
                offset += read;
                count -= read;
                totalRead += read;

                if (count == 0)
                {
                    break;
                }

                if (!RefillBufferIfNeeded())
                {
                    break;
                }
            }

            return totalRead;
        }

        public override int ReadByte()
        {
            if (NeedsRefill && !RefillBufferIfNeeded())
            {
                return -1;
            }

            int b = buffer[readPosition++];
            return b;
        }

        private bool NeedsRefill => readPosition == readLength;

        private bool RefillBufferIfNeeded()
        {
            if (!NeedsRefill)
            {
                return true;
            }

            RefillBuffer();
            if (readLength == 0)
            {
                return false;
            }

            readPosition = 0;
            return true;
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