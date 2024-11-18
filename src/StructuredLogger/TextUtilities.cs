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
                var ch = text[i];
                if (ch == '\r')
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
                else if (ch == '\n')
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
                        previousWasCarriageReturn = false;
                    }

                    currentLineLength++;
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

        public static string GetFirstLine(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            int newline = text.AsSpan().IndexOfAny('\r', '\n');
            return newline >= 0 ? text.Substring(0, newline) : text;
        }

        /// <summary>
        /// Splits a string into words by spaces, keeping quoted strings as a single token
        /// </summary>
        public static IReadOnlyList<string> Tokenize(this string text)
        {
            var result = new List<string>();

            StringBuilder currentWord = new StringBuilder();
            bool isInParentheses = false;
            bool isInQuotes = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case ' ' when !isInParentheses && !isInQuotes:
                        var wordToAdd = currentWord.ToString();
                        if (!string.IsNullOrWhiteSpace(wordToAdd))
                        {
                            result.Add(wordToAdd);
                        }

                        currentWord.Clear();
                        break;
                    case '(' when !isInParentheses && !isInQuotes:
                        isInParentheses = true;
                        currentWord.Append(c);
                        break;
                    case ')' when isInParentheses && !isInQuotes:
                        isInParentheses = false;
                        currentWord.Append(c);
                        break;
                    case '"' when !isInParentheses:
                        isInQuotes = !isInQuotes;
                        currentWord.Append(c);
                        break;
                    default:
                        currentWord.Append(c);
                        break;
                }
            }

            var word = currentWord.ToString();
            if (!string.IsNullOrWhiteSpace(word))
            {
                result.Add(word);
            }

            return result;
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
#if NET
            int i = text.AsSpan(span.Start, span.Length).IndexOf(ch);
            if (i >= 0)
            {
                return i + span.Start;
            }
#else
            for (int i = span.Start; i < span.End; i++)
            {
                if (text[i] == ch)
                {
                    return i;
                }
            }
#endif

            return -1;
        }

        public static bool IsLineBreakChar(this char c)
        {
            return c == '\r' || c == '\n';
        }

        public static string NormalizeLineBreaks(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            text =
#if NET
                text.ReplaceLineEndings("\n");
#else
                text.Replace("\r\n", "\n").Replace("\r", "\n");
#endif

            return text;
        }

        public static string TrimQuotes(this string word)
        {
            if (word != null && word.Length >= 2 && word[0] == '"' && word[word.Length - 1] == '"')
            {
                word = word.Substring(1, word.Length - 2);
            }

            return word;
        }

        public static string QuoteIfNeeded(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (text.IndexOf(' ') >= 0)
            {
                text = "\"" + text + "\"";
            }

            return text;
        }

        public static bool ContainsLineBreak(string text)
        {
            return text.IndexOf('\n') != -1;
        }

        public const int MaxDisplayedValueLength = 260;
        private const string TrimPrompt = "... (Space: view, Ctrl+C: copy)";

        public static int GetShortenLength(string text, int maxChars = MaxDisplayedValueLength)
        {
            if (text == null)
            {
                return 0;
            }

            int lineBreak = text.IndexOfFirstLineBreak();
            if (lineBreak != -1)
            {
                if (lineBreak < maxChars)
                {
                    return lineBreak;
                }
            }
            else
            {
                if (text.Length <= maxChars)
                {
                    return text.Length;
                }
            }

            return maxChars;
        }

        public static string ShortenValue(string text, string trimPrompt = TrimPrompt, int maxChars = MaxDisplayedValueLength)
        {
            if (text == null)
            {
                return text;
            }

            int newLength = GetShortenLength(text, maxChars);

            if (text.Length != newLength)
            {
                if (newLength <= maxChars && IsWhitespace(text, new Span(newLength, text.Length - newLength)))
                {
                    trimPrompt = "\u21b5";
                }

#if NET
                return string.Concat(text.AsSpan(0, newLength), trimPrompt);
#else
                return string.Concat(text.Substring(0, newLength), trimPrompt);
#endif
            }

            return text;
        }

        public static int IndexOfFirstLineBreak(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            return text.AsSpan().IndexOfAny('\r', '\n');
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

        public static (string first, string rest) GetFirstAndRest(this string text, char separator)
        {
            if (string.IsNullOrEmpty(text))
            {
                return (text, "");
            }

            int index = text.IndexOf(separator);
            if (index == -1)
            {
                return (text, "");
            }

            return (text.Substring(0, index), text.Substring(index + 1, text.Length - index - 1));
        }

        public static int GetNumberOfLeadingSpaces(string line)
        {
#if NET
            int i = line.AsSpan().IndexOfAnyExcept(' ');
            return i >= 0 ? i : line.Length;
#else
            int result = 0;
            while (result < line.Length && line[result] == ' ')
            {
                result++;
            }

            return result;
#endif
        }

        public static bool IsWhitespace(string text, Span span)
        {
            if (text != null)
            {
                for (int i = span.Start; i < span.End; i++)
                {
                    if (!char.IsWhiteSpace(text[i]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static int GetNumberOfLeadingSpaces(string text, Span span)
        {
            ReadOnlySpan<char> textSpan = text.AsSpan(span.Start, span.Length);
#if NET
            int i = textSpan.IndexOfAnyExcept(' ');
            if (i >= 0)
            {
                return i;
            }
#else
            for (int i = 0; i < textSpan.Length; i++)
            {
                if (textSpan[i] != ' ')
                {
                    return i;
                }
            }
#endif

            return textSpan.Length;
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

        public static string DisplayDuration(TimeSpan span, bool showZero = false)
        {
            if (span.TotalMilliseconds < 1)
            {
                return showZero ? "0 ms" : "";
            }

            if (span.TotalHours >= 1)
            {
                return span.ToString();
            }

            if (span.TotalMinutes >= 1)
            {
                return span.ToString(@"m\:ss\.fff");
            }

            if (span.TotalSeconds >= 1)
            {
                return span.ToString(@"s\.fff") + " s";
            }

            return span.Milliseconds + " ms";
        }

        public static string Display(this DateTime time, bool displayDate = false, bool fullPrecision = false)
        {
            string fullPrecisionString = fullPrecision ? "ffff" : "";

            var formatString = "HH:mm:ss.fff" + fullPrecisionString;
            if (displayDate)
            {
                formatString = "yyyy-MM-dd HH:mm:ss.fff" + fullPrecisionString;
            }

            return time.ToString(formatString);
        }

        private static readonly HashSet<char> invalidFileNameChars = new(Path.GetInvalidFileNameChars());

        public static string SanitizeFileName(string text)
        {
            const int maxLength = 40;

            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            ReadOnlySpan<char> textSpan = text.AsSpan();
            if (text.Length > maxLength)
            {
                textSpan = textSpan.Slice(0, maxLength);
            }

            Span<char> chars = stackalloc char[maxLength];
            for (int i = 0; i < textSpan.Length; i++)
            {
                char ch = textSpan[i];
                if (invalidFileNameChars.Contains(ch))
                {
                    ch = '_';
                }

                chars[i] = ch;
            }

            return chars.Slice(0, textSpan.Length).ToString();
        }

        public static string NormalizeFilePath(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

#if NET
            int slashCount = text.AsSpan().Count('/');
            int backslashCount = text.AsSpan().Count('\\');
#else
            int slashCount = text.Count(c => c == '/');
            int backslashCount = text.Count(c => c == '\\');
#endif

            if (slashCount == 0 && backslashCount == 0)
            {
                return text;
            }

            bool isWindows = false;

            if (text.Length > 1 && text[1] == ':')
            {
                isWindows = true;
            }
            else
            {
                if (backslashCount > slashCount)
                {
                    isWindows = true;
                }
            }

            bool startsWithTwoBackslashes = text.Length > 2 && text[0] == '\\' && text[1] == '\\';

            // hack
            // recognize paths such as \a\b\c as /a/b/c since that's what the binary logger gives us
            if (slashCount == 0 && text.Length > 1 && text[0] == '\\' && text[1] != '\\' && text[1] != '/')
            {
                isWindows = false;
            }

            string directorySeparator;
            string altDirectorySeparator;
            if (isWindows)
            {
                directorySeparator = "\\";
                altDirectorySeparator = "/";
            }
            else
            {
                directorySeparator = "/";
                altDirectorySeparator = "\\";
            }

            text = text.Replace(altDirectorySeparator, directorySeparator);

            var parts = text.Split(new[] { directorySeparator[0] }, StringSplitOptions.RemoveEmptyEntries);

            var stack = new List<string>();

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (stack.Count > 0 && parts[i] == "..")
                {
                    stack.RemoveAt(stack.Count - 1);
                }
                else
                {
                    stack.Add(part);
                }
            }

            text = string.Join(directorySeparator, stack);

            if (!isWindows)
            {
                text = '/' + text;
            }
            else if (text.Length > 1 && text[1] != ':')
            {
                text = "\\" + text;
            }

            if (isWindows && startsWithTwoBackslashes)
            {
                text = "\\" + text;
            }

            return text;
        }

        public static IReadOnlyList<Span> GetHighlightedSpansInText(string text, IEnumerable<string> searchTerms)
        {
            if (searchTerms == null || !searchTerms.Any())
            {
                return Array.Empty<Span>();
            }

            var spans = new List<Span>();

            foreach (var searchTerm in searchTerms)
            {
                int index = 0;
                while (true)
                {
                    index = text.IndexOf(searchTerm, index, StringComparison.OrdinalIgnoreCase);
                    if (index == -1)
                    {
                        break;
                    }
                    else
                    {
                        spans.Add(new Span(index, searchTerm.Length));
                        index += searchTerm.Length;
                    }
                }
            }

            return NormalizeSpans(spans);
        }

        public static IReadOnlyList<Span> NormalizeSpans(IReadOnlyList<Span> spans)
        {
            if (spans == null || spans.Count == 0)
            {
                return spans;
            }

            var final = new List<Span>();

            var sorted = spans.OrderBy(s => s.Start).ThenByDescending(s => s.Length);

            Span current = sorted.First();

            foreach (var span in sorted)
            {
                if (current.ContainsEndInclusive(span.Start))
                {
                    if (span.End > current.End)
                    {
                        current.Length = span.End - current.Start;
                    }
                }
                else
                {
                    final.Add(current);
                    current = span;
                }
            }

            final.Add(current);

            return final;
        }

        public static IReadOnlyList<string> SplitIntoParenthesizedSpans(string text, string openParen, string closeParen)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            var list = new List<string>();

            void Add(int start, int end)
            {
                if (end > start)
                {
                    var chunk = text.Substring(start, end - start);
                    list.Add(chunk);
                }
            }

            int previous = 0;
            for (int i = 0; i > -1 && i < text.Length; )
            {
                i = text.IndexOf(openParen, i);
                if (i == -1)
                {
                    Add(previous, text.Length);
                    return list;
                }

                Add(previous, i);
                previous = i;

                i = text.IndexOf(closeParen, i);
                if (i == -1)
                {
                    Add(previous, text.Length);
                    return list;
                }

                i += closeParen.Length;

                Add(previous, i);
                previous = i;
            }

            Add(previous, text.Length);
            return list;
        }
    }
}
