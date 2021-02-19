using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class SaveStrings
    {
        public void Run(string binLogFilePath, string outputFilePath)
        {
            var build = BinaryLog.ReadBuild(binLogFilePath);
            var strings = build.StringTable.Instances.OrderBy(s => s).ToArray();

            WriteStrings(outputFilePath, strings);
        }

        private static void WriteStrings(string outputFilePath, string[] strings)
        {
            using var fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
            using var bufferedStream = new BufferedStream(gzipStream);
            using var binaryWriter = new BinaryWriter(bufferedStream);

            binaryWriter.Write(strings.Length);

            for (int i = 0; i < strings.Length; i++)
            {
                binaryWriter.Write(strings[i]);
            }
        }

        public static IReadOnlyList<string> ReadStrings(string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var bufferedStream = new BufferedStream(gzipStream);
            using var binaryReader = new BinaryReader(bufferedStream);

            int count = binaryReader.ReadInt32();
            var result = new string[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = binaryReader.ReadString();
            }

            return result;
        }
    }
}