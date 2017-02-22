using System.IO;
using System.IO.Compression;

namespace Microsoft.Build.Logging
{
    public class BinaryLogReplayEventSource : EventArgsDispatcher
    {
        public void Replay(string sourceFilePath)
        {
            using (var stream = new FileStream(sourceFilePath, FileMode.Open))
            {
                var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
                var binaryReader = new BinaryReader(gzipStream);

                byte fileFormatVersion = binaryReader.ReadByte();

                var reader = new BuildEventArgsReader(binaryReader);
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
