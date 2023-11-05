using System;
using System.Collections.Generic;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Language.Xml;

namespace StructuredLogViewer
{
    public class SourceText
    {
        public SourceText(string text)
        {
            Text = text;
        }

        public string Text { get; }

        private IReadOnlyList<Span> lines;
        public IReadOnlyList<Span> Lines
        {
            get
            {
                if (lines == null)
                {
                    lines = Text.GetLineSpans();
                }

                return lines;
            }
        }

        private XmlDocumentSyntax xmlRoot;
        public XmlDocumentSyntax XmlRoot
        {
            get
            {
                if (xmlRoot == null)
                {
                    xmlRoot = Parser.ParseText(Text);
                }

                return xmlRoot;
            }
        }

        public IReadOnlyList<int> Find(string searchText)
        {
            var result = new List<int>();
            var searchTextLength = searchText.Length;
            for (int i = 0; i < Lines.Count; i++)
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
            if (line.Length == 0)
            {
                return "";
            }

            var end = line.End - 1;
            while (end >= line.Start && Text[end].IsLineBreakChar())
            {
                end--;
            }

            if (end < line.Start)
            {
                return "";
            }

            return Text.Substring(line.Start, end - line.Start + 1);
        }

        public int GetLineNumberFromPosition(int startPosition)
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                if (startPosition >= Lines[i].Start && startPosition < Lines[i].End)
                {
                    return i;
                }
            }

            return 0;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
