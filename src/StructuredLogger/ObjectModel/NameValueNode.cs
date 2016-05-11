namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NameValueNode : ParentedNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string NameAndEquals => Name + " = ";
        public string ShortenedValue => ShortenValue(Value);

        private const int MaxDisplayedValueLength = 260;
        private const string TrimPrompt = "... (Ctrl+C to copy full text)";

        private string ShortenValue(string text)
        {
            if (text == null || text.Length < MaxDisplayedValueLength)
            {
                return text;
            }

            int lineBreak = text.IndexOf('\n');
            if (lineBreak > -1 && lineBreak < MaxDisplayedValueLength)
            {
                for (int i = 0; i < lineBreak - 1; i++)
                {
                    if (!char.IsWhiteSpace(text[i]))
                    {
                        return text.Substring(0, lineBreak) + TrimPrompt;
                    }
                }
            }

            return text.Substring(0, MaxDisplayedValueLength) + TrimPrompt;
        }

        public override string ToString() => Name + " = " + Value;
        public bool IsVisible { get { return true; } set { } }
        public bool IsExpanded { get { return true; } set { } }
    }
}
