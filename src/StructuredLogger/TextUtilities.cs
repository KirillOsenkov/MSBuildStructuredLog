using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public static class TextUtilities
    {
        private static readonly IReadOnlyList<Span> Empty = new Span[] { Span.Empty };

        private static readonly char[] semicolonCharArray = { ';' };

        public static IReadOnlyList<string> SplitSemicolonDelimitedList(string list) => list.Split(semicolonCharArray);

        public static void CollectLineSpans(this string text, ICollection<Span> spans, bool includeLineBreakInSpan = true)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (spans == null)
            {
                throw new ArgumentNullException(nameof(spans));
            }

            if (text.Length == 0)
            {
                return;
            }

            int currentPosition = 0;
            int currentLineLength = 0;
            bool previousWasCarriageReturn = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (previousWasCarriageReturn)
                    {
                        int lineLengthIncludingLineBreak = currentLineLength;
                        if (!includeLineBreakInSpan)
                        {
                            currentLineLength--;
                        }

                        spans.Add(new Span(currentPosition, currentLineLength));

                        currentPosition += lineLengthIncludingLineBreak;
                        currentLineLength = 1;
                    }
                    else
                    {
                        currentLineLength++;
                        previousWasCarriageReturn = true;
                    }
                }
                else if (text[i] == '\n')
                {
                    var lineLength = currentLineLength;
                    if (previousWasCarriageReturn)
                    {
                        lineLength--;
                    }

                    currentLineLength++;
                    previousWasCarriageReturn = false;
                    if (includeLineBreakInSpan)
                    {
                        lineLength = currentLineLength;
                    }

                    spans.Add(new Span(currentPosition, lineLength));
                    currentPosition += currentLineLength;
                    currentLineLength = 0;
                }
                else
                {
                    if (previousWasCarriageReturn)
                    {
                        var lineLength = currentLineLength;
                        if (!includeLineBreakInSpan)
                        {
                            lineLength--;
                        }

                        spans.Add(new Span(currentPosition, lineLength));
                        currentPosition += currentLineLength;
                        currentLineLength = 0;
                    }

                    currentLineLength++;
                    previousWasCarriageReturn = false;
                }
            }

            var finalLength = currentLineLength;
            if (previousWasCarriageReturn && !includeLineBreakInSpan)
            {
                finalLength--;
            }

            spans.Add(new Span(currentPosition, finalLength));

            if (previousWasCarriageReturn)
            {
                spans.Add(new Span(currentPosition, 0));
            }
        }

        public static IReadOnlyList<Span> GetLineSpans(this string text, bool includeLineBreakInSpan = true)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.Length == 0)
            {
                return Empty;
            }

            var result = new List<Span>();
            text.CollectLineSpans(result, includeLineBreakInSpan);
            return result.ToArray();
        }

        public static IReadOnlyList<string> GetLines(this string text, bool includeLineBreak = false)
        {
            return GetLineSpans(text, includeLineBreakInSpan: includeLineBreak)
                .Select(span => text.Substring(span.Start, span.Length))
                .ToArray();
        }

        public static string Substring(this string text, Span span)
        {
            return text.Substring(span.Start, span.Length);
        }

        public static bool Contains(this string text, Span span, char ch)
        {
            return IndexOf(text, span, ch) != -1;
        }

        public static int IndexOf(this string text, Span span, char ch)
        {
            for (int i = span.Start; i < span.End; i++)
            {
                if (text[i] == ch)
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool IsLineBreakChar(this char c)
        {
            return c == '\r' || c == '\n';
        }

        public static bool ContainsLineBreak(string text)
        {
            return text.IndexOf('\n') != -1;
        }

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

        public static bool IsWhitespace(string text, Span span)
        {
            if (text == null)
            {
                return true;
            }

            for (int i = span.Start; i < span.End; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return false;
                }
            }

            return true;
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

        public static string Display(DateTime time, bool displayDate = false)
        {
            var formatString = "HH:mm:ss.fff";
            if (displayDate)
            {
                formatString = "yyyy-MM-dd HH:mm:ss.fff";
            }

            return time.ToString(formatString);
        }

        private static char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

        public static string SanitizeFileName(string text)
        {
            const int maxLength = 40;

            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (text.Length > maxLength)
            {
                text = text.Substring(0, maxLength);
            }

            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (invalidFileNameChars.Contains(ch))
                {
                    ch = '_';
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }
    }
}