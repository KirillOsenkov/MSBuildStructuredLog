using System;
using System.Collections.Generic;
using System.Linq;
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

        public IXmlElement RootElement
        {
            get
            {
                var root = XmlRoot.Root;

                // work around a bug in Xml Parser where a virtual parent is created around the root element
                // when the root element is preceded by trivia (comment)
                if (root.Name == null && root.Elements.FirstOrDefault() is IXmlElement firstElement && firstElement.Name == "Project")
                {
                    root = firstElement;
                }

                return root;
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

        public string GetText(TextSpan span)
        {
            return Text.Substring(span.Start, span.Length);
        }

        public string GetText(int start, int length)
        {
            return Text.Substring(start, length);
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

        public (int Line, int Column) GetLineAndColumn1Based(int position)
        {
            var lineNumber = GetLineNumberFromPosition(position);
            var column = position - Lines[lineNumber].Start + 1;
            var line = lineNumber + 1;
            return (line, column);
        }

        public override string ToString()
        {
            return Text;
        }

        public int GetPosition(int lineNumber1Based, int columnNumber1Based)
        {
            if (lineNumber1Based >= 1 && lineNumber1Based <= Lines.Count)
            {
                var line = Lines[lineNumber1Based - 1];
                if (columnNumber1Based <= line.Length)
                {
                    return line.Start + columnNumber1Based - 1;
                }
            }

            return -1;
        }
    }
}
