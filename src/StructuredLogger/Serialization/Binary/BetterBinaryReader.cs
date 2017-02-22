using System.IO;

namespace Microsoft.Build.Logging.Serialization
{
    public class BetterBinaryReader : BinaryReader
    {
        public BetterBinaryReader(Stream input) : base(input)
        {
        }

        public override int ReadInt32()
        {
            return Read7BitEncodedInt();
        }
    }
}
