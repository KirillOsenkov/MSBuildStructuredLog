using System;
using System.Collections.Generic;

namespace StructuredLogViewer
{
    public static class TextUtilities
    {
        public static int[] GetLineLengths(this string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException();
            }

            if (text.Length == 0)
            {
                return new int[0];
            }

            var result = new List<int>();
            int currentLineLength = 0;
            bool previousWasCarriageReturn = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (previousWasCarriageReturn)
                    {
                        currentLineLength++;
                        result.Add(currentLineLength);
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
                    result.Add(currentLineLength);
                    currentLineLength = 0;
                }
                else
                {
                    currentLineLength++;
                    previousWasCarriageReturn = false;
                }
            }

            result.Add(currentLineLength);

            if (previousWasCarriageReturn)
            {
                result.Add(0);
            }

            return result.ToArray();
        }

        public static bool IsLineBreakChar(this char c)
        {
            return c == '\r' || c == '\n';
        }
    }
}