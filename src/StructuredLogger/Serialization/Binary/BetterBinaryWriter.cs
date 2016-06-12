using System.IO;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BetterBinaryWriter : BinaryWriter
    {
        public BetterBinaryWriter(Stream output) : base(output)
        {
        }

        public override void Write(int value)
        {
            Write7BitEncodedInt(value);
        }
    }
}
