using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TreeBinaryReader : IDisposable
    {
        private readonly string filePath;
        private readonly BinaryReader binaryReader;
        private readonly FileStream fileStream;
        private readonly GZipStream gzipStream;
        private readonly string[] stringTable;
        private readonly List<string> attributes = new List<string>(10);

        public TreeBinaryReader(string filePath)
        {
            this.filePath = filePath;
            this.fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            int major = fileStream.ReadByte();
            int minor = fileStream.ReadByte();
            int revision = fileStream.ReadByte();
            this.gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            this.binaryReader = new BetterBinaryReader(gzipStream);

            var count = binaryReader.ReadInt32();
            stringTable = new string[count];
            for (int i = 0; i < count; i++)
            {
                stringTable[i] = binaryReader.ReadString();
            }
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
            binaryReader.Dispose();
        }
    }
}
