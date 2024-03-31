namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TextNode : TreeNode
    {
        public string Text { get; set; }
        public string ShortenedText => TextUtilities.ShortenValue(Text);
        public bool IsTextShortened => Text != null && Text.Length != TextUtilities.GetShortenLength(Text);


        public override string TypeName => nameof(TextNode);
        public override string Title => Text ?? TypeName;

        public override string ToString()
        {
            return Title;
        }
    }
}
