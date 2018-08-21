﻿using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public static class Utilities
    {
        public const int MaxDisplayedValueLength = 260;
        private const string TrimPrompt = "... (Space: view, Ctrl+C: copy)";

        public static string ShortenValue(string text, string trimPrompt = TrimPrompt, int maxChars = MaxDisplayedValueLength)
        {
            if (text == null)
            {
                return text;
            }

            int newLength = maxChars;
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

        public static KeyValuePair<string, string> ParseNameValue(string largeText, Span span)
        {
            var equals = largeText.IndexOf(span, '=');
            if (equals == -1)
            {
                return new KeyValuePair<string, string>(largeText.Substring(span), "");
            }

            return ParseNameValueWithEqualsPosition(largeText, span, equals);
        }

        public static KeyValuePair<string, string> ParseNameValueWithEqualsPosition(string largeText, Span span, int equals)
        {
            var name = largeText.Substring(span.Start, equals - span.Start);
            var value = largeText.Substring(equals + 1, span.End - equals - 1);
            return new KeyValuePair<string, string>(name, value);
        }

        public static KeyValuePair<string, string> ParseNameValue(string nameEqualsValue, int trimFromStart = 0)
        {
            var equals = nameEqualsValue.IndexOf('=');
            if (equals == -1)
            {
                return new KeyValuePair<string, string>(nameEqualsValue, "");
            }

            return ParseNameValue(nameEqualsValue, trimFromStart, equals);
        }

        public static KeyValuePair<string, string> ParseNameValue(string nameEqualsValue, int trimFromStart, int equals)
        {
            var name = nameEqualsValue.Substring(trimFromStart, equals - trimFromStart);
            var value = nameEqualsValue.Substring(equals + 1);
            return new KeyValuePair<string, string>(name, value);
        }

        public static KeyValuePair<string, string> ParseNameValueWithEqualsPosition(string nameEqualsValue, int equals)
        {
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

        public static int GetNumberOfLeadingSpaces(string text, Span span)
        {
            int index = span.Start;
            while (index < span.End && text[index] == ' ')
            {
                index++;
            }

            return index - span.Start;
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

        public static string DisplayDuration(TimeSpan span)
        {
            if (span.TotalMilliseconds < 1)
            {
                return "";
            }

            if (span.TotalSeconds > 3600)
            {
                return span.ToString(@"h\:mm\:ss");
            }

            if (span.TotalSeconds > 60)
            {
                return span.ToString(@"m\:ss\.fff");
            }

            if (span.TotalMilliseconds > 1000)
            {
                return span.ToString(@"s\.fff") + " s";
            }

            return span.Milliseconds + " ms";
        }
    }
}
