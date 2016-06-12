using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TreeBinaryWriter : IDisposable
    {
        private readonly string filePath;
        private readonly BinaryWriter binaryWriter;
        private readonly FileStream fileStream;
        private readonly GZipStream gzipStream;
        private readonly Dictionary<string, int> stringTable = new Dictionary<string, int>();
        private readonly List<string> attributes = new List<string>(10);

        public TreeBinaryWriter(string filePath)
        {
            this.filePath = filePath;
            this.fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            this.gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
            this.binaryWriter = new BetterBinaryWriter(gzipStream);
        }

        public void WriteNode(string name)
        {
            attributes.Clear();
            binaryWriter.Write(GetStringIndex(name));
        }

        public void WriteAttributeValue(string value)
        {
            attributes.Add(value);
        }

        public void WriteEndAttributes()
        {
            binaryWriter.Write(attributes.Count);
            foreach (var attributeValue in attributes)
            {
                binaryWriter.Write(GetStringIndex(attributeValue));
            }
        }

        public void WriteChildrenCount(int count)
        {
            binaryWriter.Write(count);
        }

        public void WriteEndNode()
        {
        }

        private int GetStringIndex(string text)
        {
            if (text == null)
            {
                return 0;
            }

            lock (stringTable)
            {
                int index = 0;
                if (stringTable.TryGetValue(text, out index))
                {
                    return index;
                }

                index = stringTable.Count + 1;
                stringTable[text] = index;
                return index;
            }
        }

        private void WriteStringTable()
        {
            binaryWriter.Write(stringTable.Count);
            foreach (var entry in stringTable.OrderBy(kvp => kvp.Value))
            {
                binaryWriter.Write(entry.Key);
            }
        }

        public void Dispose()
        {
            WriteStringTable();
            binaryWriter.Dispose();
        }
    }
}
