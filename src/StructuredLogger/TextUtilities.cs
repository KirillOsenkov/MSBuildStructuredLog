using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public static class TextUtilities
    {
        public static Span[] GetLineSpans(this string text, bool includeLineBreakInSpan = true)
        {
            if (text == null)
            {
                throw new ArgumentNullException();
            }

            if (text.Length == 0)
            {
                return Array.Empty<Span>();
            }

            var result = new List<Span>();
            int currentPosition = 0;
            int currentLineLength = 0;
            bool previousWasCarriageReturn = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (previousWasCarriageReturn)
                    {
                        currentLineLength++;
                        previousWasCarriageReturn = false;
                        var lineLength = currentLineLength;
                        if (!includeLineBreakInSpan)
                        {
                            lineLength--;
                        }

                        result.Add(new Span(currentPosition, lineLength));
                        currentPosition += currentLineLength;
                        currentLineLength = 0;
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

                    result.Add(new Span(currentPosition, lineLength));
                    currentPosition += currentLineLength;
                    currentLineLength = 0;
                }
                else
                {
                    currentLineLength++;
                    previousWasCarriageReturn = false;
                }
            }

            result.Add(new Span(currentPosition, currentLineLength));

            if (previousWasCarriageReturn)
            {
                result.Add(new Span(currentPosition, 0));
            }

            return result.ToArray();
        }

        public static string[] GetLines(this string text)
        {
            return GetLineSpans(text, includeLineBreakInSpan: false)
                .Select(span => text.Substring(span.Start, span.Length))
                .ToArray();
        }

        public static bool IsLineBreakChar(this char c)
        {
            return c == '\r' || c == '\n';
        }
    }
}