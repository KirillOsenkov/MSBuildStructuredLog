using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TreeBinaryReader : IDisposable
    {
        private BinaryReader binaryReader;
        private Stream fileStream;
        private GZipStream gzipStream;
        private string[] stringTable;

        public TreeBinaryReader(string filePath)
        {
            this.fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Initialize(fileStream);
        }

        public TreeBinaryReader(Stream stream)
        {
            Initialize(stream);
        }

        public TreeBinaryReader(Stream stream, Version version)
        {
            Initialize(stream, version);
        }

        private void Initialize(Stream stream, Version version = null)
        { 
            this.fileStream = stream;
            if (fileStream.Length < 8)
            {
                // file is too short to be valid
                fileStream.Dispose();
                fileStream = null;
                return;
            }

            int major, minor, build;

            if (version == null)
            {
                major = fileStream.ReadByte();
                minor = fileStream.ReadByte();
                build = fileStream.ReadByte();
            }
            else
            {
                major = version.Major;
                minor = version.Minor;
                build = version.Build;
            }

            Version = new Version(major, minor, build, 0);

            if (major < 1 || major > 2)
            {
                // invalid or unsupported file format
                fileStream.Dispose();
                fileStream = null;
                return;
            }

            try
            {
                this.gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                var bufferedStream = new BufferedStream(gzipStream, 32768);
                this.binaryReader = new BetterBinaryReader(bufferedStream);

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

            if (fileStream != null)
            {
                fileStream.Dispose();
                fileStream = null;
            }
        }
    }
}
