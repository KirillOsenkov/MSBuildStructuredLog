namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TextNode : NamedNode
    {
        public string Text { get; set; }
        public string ShortenedText => Utilities.ShortenValue(Text);
        public bool IsTextShortened => Text != ShortenedText;

        public override string ToString()
        {
            var baseText = base.ToString();
            if (string.IsNullOrEmpty(baseText))
            {
                return Text;
            }

            return baseText + " " + Text;
        }
    }
}
