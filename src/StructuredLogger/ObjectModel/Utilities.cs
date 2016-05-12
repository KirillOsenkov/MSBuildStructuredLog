namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Utilities
    {
        public const int MaxDisplayedValueLength = 260;
        private const string TrimPrompt = "... (Ctrl+C to copy full text)";

        public static string ShortenValue(string text, string trimPrompt = TrimPrompt)
        {
            if (text == null)
            {
                return text;
            }

            int newLength = MaxDisplayedValueLength;
            int lineBreak = text.IndexOf('\n');
            if (lineBreak == -1)
            {
                if (text.Length <= newLength)
                {
                    return text;
                }
            }
            else
            {
                if (lineBreak < newLength)
                {
                    newLength = lineBreak;
                }
            }

            return text.Substring(0, newLength) + TrimPrompt;
        }
    }
}
