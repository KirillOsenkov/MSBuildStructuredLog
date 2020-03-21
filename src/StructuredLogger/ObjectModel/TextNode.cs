namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TextNode : NamedNode
    {
        public string Text { get; set; }
        public string ShortenedText => TextUtilities.ShortenValue(Text);
        public bool IsTextShortened => Text != ShortenedText;

        protected override string GetTitle() => base.GetTitle() ?? Text;
        public override string TypeName => nameof(TextNode);

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
