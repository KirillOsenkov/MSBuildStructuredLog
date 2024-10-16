using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TreeBinaryWriter : IDisposable
    {
        private readonly string filePath;
        private readonly BinaryWriter binaryWriter;
        private readonly BetterBinaryWriter treeNodesStreamBinaryWriter;
        private readonly FileStream fileStream;
        private readonly GZipStream gzipStream;

        /// <summary>
        /// We first write all nodes of the tree to the temporary memory stream,
        /// then write the string table to the actual destination stream, then
        /// write the nodes from memory to the actual stream.
        /// String table has to come first because we need to have it already
        /// when we start reading the nodes later.
        /// </summary>
        private readonly MemoryStream treeNodesStream;
        private readonly Dictionary<string, int> stringTable = new Dictionary<string, int>();
        private readonly List<string> attributes = new List<string>(10);

        public TreeBinaryWriter(string filePath)
        {
            this.filePath = filePath;
            this.treeNodesStream = new MemoryStream();
            this.fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            WriteVersion();
            this.gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
            this.binaryWriter = new BetterBinaryWriter(DestinationStream);
            this.treeNodesStreamBinaryWriter = new BetterBinaryWriter(treeNodesStream);
        }

        private void WriteVersion()
        {
            // hardcode this version for now because viewers don't expect major version to be > 1
            fileStream.WriteByte(1);
            fileStream.WriteByte(2);
            fileStream.WriteByte(48);
        }

        public void WriteNode(string name)
        {
            attributes.Clear();
            treeNodesStreamBinaryWriter.Write(GetStringIndex(name));
        }

        public void WriteAttributeValue(string value)
        {
            attributes.Add(value);
        }

        public void WriteEndAttributes()
        {
            treeNodesStreamBinaryWriter.Write(attributes.Count);
            foreach (var attributeValue in attributes)
            {
                treeNodesStreamBinaryWriter.Write(GetStringIndex(attributeValue));
            }
        }

        public void WriteChildrenCount(int count)
        {
            treeNodesStreamBinaryWriter.Write(count);
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

        public void WriteByteArray(byte[] bytes)
        {
            bytes ??= [];
            treeNodesStreamBinaryWriter.Write(bytes.Length);
            if (bytes.Length > 0)
            {
                treeNodesStreamBinaryWriter.Write(bytes);
            }
        }

        private Stream DestinationStream => gzipStream;

        public void Dispose()
        {
            WriteStringTable();
            treeNodesStream.Position = 0;
            treeNodesStream.CopyTo(DestinationStream);
            binaryWriter.Dispose();
        }
    }
}
