namespace Microsoft.Build.Logging.StructuredLogger
{
    public struct Span
    {
        public int Start;
        public int Length;
        public int End => Start + Length;

        public static readonly Span Empty = new Span();

        public Span(int start, int length) : this()
        {
            Start = start;
            Length = length;
        }

        public override string ToString()
        {
            return $"({Start}, {Length})";
        }
    }
}
