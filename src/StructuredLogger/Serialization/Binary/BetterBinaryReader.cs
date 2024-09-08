namespace Microsoft.Build.Logging.StructuredLogger
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
