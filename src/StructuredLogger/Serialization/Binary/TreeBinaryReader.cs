using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TreeBinaryReader : IDisposable
    {
        private readonly string filePath;
        private BinaryReader binaryReader;
        private readonly FileStream fileStream;
        private readonly GZipStream gzipStream;
        private readonly string[] stringTable;
        private readonly List<string> attributes = new List<string>(10);

        public TreeBinaryReader(string filePath)
        {
            this.filePath = filePath;
            this.fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            if (fileStream.Length < 8)
            {
                // file is too short to be valid
                fileStream.Dispose();
                fileStream = null;
                return;
            }

            int major = fileStream.ReadByte();
            int minor = fileStream.ReadByte();
            int build = fileStream.ReadByte();
            Version = new Version(major, minor, build, 0);

            if (major != 1)
            {
                // invalid or unsupported file format
                fileStream.Dispose();
                fileStream = null;
                return;
            }

            try
            {
                this.gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                this.binaryReader = new BetterBinaryReader(gzipStream);

                var count = binaryReader.ReadInt32();
                stringTable = new string[count];
                for (int i = 0; i < count; i++)
                {
                    stringTable[i] = binaryReader.ReadString();
                }
            }
            catch (Exception)
            {
                if (binaryReader != null)
                {
                    binaryReader.Dispose();
                    binaryReader = null;
                }
                else if (fileStream != null)
                {
                    fileStream.Dispose();
                    fileStream = null;
                }
            }
        }

        public bool IsValid()
        {
            return binaryReader != null;
        }

        public Version Version { get; private set; }

        public string[] StringTable => stringTable;

        public byte[] ReadByteArray()
        {
            int length = binaryReader.ReadInt32();
            if (length > 0)
            {
                return binaryReader.ReadBytes(length);
            }

            return null;
        }

        public void ReadStringArray(Queue<string> array)
        {
            array.Clear();

            var count = binaryReader.ReadInt32();
            if (count == 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                array.Enqueue(ReadString());
            }
        }

        public string ReadString()
        {
            return GetString(binaryReader.ReadInt32());
        }

        public int ReadInt32()
        {
            return binaryReader.ReadInt32();
        }

        private string GetString(int index)
        {
            if (index == 0)
            {
                return null;
            }

            return stringTable[index - 1];
        }

        public void Dispose()
        {
            if (binaryReader != null)
            {
                binaryReader.Dispose();
                binaryReader = null;
            }
        }
    }
}
