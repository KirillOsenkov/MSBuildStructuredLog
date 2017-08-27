namespace StructuredLogViewer
{
    public struct Span
    {
        public int Start;
        public int Length;
        public int End => Start + Length;

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
