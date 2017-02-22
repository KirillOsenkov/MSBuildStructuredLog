using System.IO;
using System.IO.Compression;

namespace Microsoft.Build.Logging.Serialization
{
    public class BinaryLogReplayEventSource : EventArgsDispatcher
    {
        public void Replay(string sourceFilePath)
        {
            using (var stream = new FileStream(sourceFilePath, FileMode.Open))
            {
                var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                var binaryReader = new BinaryReader(gzipStream);

                int fileFormatVersion = binaryReader.ReadInt32();

                var reader = new EventArgsReader(binaryReader);
                while (true)
                {
                    var instance = reader.Read();
                    if (instance == null)
                    {
                        break;
                    }

                    Dispatch(instance);
                }
            }
        }
    }
}
