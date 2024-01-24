using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging
{
    public class Record
    {
        public BinaryLogRecordKind Kind;
        public byte[] Bytes;
        public BuildEventArgs Args;
        public long Start;
        public long Length;
    }

    public readonly record struct RecordInfo(BinaryLogRecordKind Kind, long Start, long Length);
}
