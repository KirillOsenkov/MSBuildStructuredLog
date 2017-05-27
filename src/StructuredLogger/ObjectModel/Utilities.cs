using System.Collections.Generic;

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

            return text.Substring(0, newLength) + trimPrompt;
        }

        public static KeyValuePair<string, string> ParseNameValue(string nameEqualsValue)
        {
            var equals = nameEqualsValue.IndexOf('=');
            if (equals == -1)
            {
                return new KeyValuePair<string, string>(nameEqualsValue, "");
            }

            var name = nameEqualsValue.Substring(0, equals);
            var value = nameEqualsValue.Substring(equals + 1);
            return new KeyValuePair<string, string>(name, value);
        }

        public static int GetNumberOfLeadingSpaces(string line)
        {
            int result = 0;
            while (result < line.Length && line[result] == ' ')
            {
                result++;
            }

            return result;
        }
    }
}
