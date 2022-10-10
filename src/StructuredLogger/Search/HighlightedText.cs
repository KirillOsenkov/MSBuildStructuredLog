namespace Microsoft.Build.Logging.StructuredLogger
{
    public class HighlightedText
    {
        public string Text { get; set; }
        public string Style { get; set; }

        public override string ToString() => Text;
    }
}
