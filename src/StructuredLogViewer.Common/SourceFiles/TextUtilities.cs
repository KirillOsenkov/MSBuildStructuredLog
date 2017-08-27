using System;
using System.Collections.Generic;

namespace StructuredLogViewer
{
    public static class TextUtilities
    {
        public static Span[] GetLineSpans(this string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException();
            }

            if (text.Length == 0)
            {
                return new Span[0];
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
                        result.Add(new Span(currentPosition, currentLineLength));
                        currentPosition += currentLineLength;
                        currentLineLength = 0;
                        previousWasCarriageReturn = false;
                    }
                    else
                    {
                        currentLineLength++;
                        previousWasCarriageReturn = true;
                    }
                }
                else if (text[i] == '\n')
                {
                    previousWasCarriageReturn = false;
                    currentLineLength++;
                    result.Add(new Span(currentPosition, currentLineLength));
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

        public static bool IsLineBreakChar(this char c)
        {
            return c == '\r' || c == '\n';
        }
    }
}