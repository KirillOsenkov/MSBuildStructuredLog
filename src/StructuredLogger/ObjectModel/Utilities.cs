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

        public static bool ContainsLineBreak(string text)
        {
            return text.IndexOf('\n') != -1;
        }

        public static KeyValuePair<string, string> ParseNameValue(string nameEqualsValue, int trimFromStart = 0)
        {
            var equals = nameEqualsValue.IndexOf('=');
            if (equals == -1)
            {
                return new KeyValuePair<string, string>(nameEqualsValue, "");
            }

            var name = nameEqualsValue.Substring(trimFromStart, equals - trimFromStart);
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

        public static string ParseQuotedSubstring(string text)
        {
            int firstQuote = text.IndexOf('"');
            if (firstQuote == -1)
            {
                return text;
            }

            int secondQuote = text.IndexOf('"', firstQuote + 1);
            if (secondQuote == -1)
            {
                return text;
            }

            if (secondQuote - firstQuote < 2)
            {
                return text;
            }

            return text.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }
    }
}
