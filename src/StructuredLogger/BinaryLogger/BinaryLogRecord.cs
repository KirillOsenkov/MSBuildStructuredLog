using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    public class Record
    {
        public byte[] Bytes;
        public BuildEventArgs Args;
        public long Start;
        public long Length;
    }
}
