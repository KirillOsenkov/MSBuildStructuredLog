using System;
using System.Collections.Generic;

namespace StructuredLogViewer
{
    public class SourceText
    {
        public SourceText(string text)
        {
            Text = text;
            Lines = TextUtilities.GetLineSpans(text);
        }

        public string Text { get; }
        public Span[] Lines { get; }

        public IReadOnlyList<int> Find(string searchText)
        {
            var result = new List<int>();
            var searchTextLength = searchText.Length;
            for (int i = 0; i < Lines.Length; i++)
            {
                var line = Lines[i];
                if (line.Length >= searchTextLength)
                {
                    int foundOffset = Text.IndexOf(searchText, line.Start, line.Length, StringComparison.OrdinalIgnoreCase);
                    if (foundOffset >= line.Start && foundOffset < line.End - searchTextLength)
                    {
                        result.Add(i);
                    }
                }
            }

            return result;
        }

        public string GetLineText(int lineNumber)
        {
            var line = Lines[lineNumber];
            var end = line.End - 1;
            while (TextUtilities.IsLineBreakChar(Text[end]) && end > line.Start)
            {
                end--;
            }

            return Text.Substring(line.Start, end - line.Start);
        }
    }
}
