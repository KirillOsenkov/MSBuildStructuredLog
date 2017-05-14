using System;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Language.Xml;

namespace StructuredLogViewer
{
    public class Classifier
    {
        private static readonly SolidColorBrush[] brushes = new[]
        {
            null, // None
            Brushes.Red, // XmlAttributeName
            null, // XmlAttributeQuotes
            Brushes.Blue, // XmlAttributeValue
            Brushes.Gray, // XmlCDataSection
            Brushes.Green, // XmlComment
            Brushes.Blue, // XmlDelimiter
            Brushes.Red, // XmlEntityReference
            Brushes.Firebrick, // XmlName new SolidColorBrush(Color.FromRgb(163, 21, 21))
            Brushes.Gray, // XmlProcessingInstruction
            null, // XmlText
        };

        public void Classify(RichTextBox richTextBox, string text)
        {
            var document = richTextBox.Document;
            Paragraph paragraph = new Paragraph();
            document.Blocks.Add(paragraph);

            if (!LooksLikeXml(text))
            {
                paragraph.Inlines.Add(text);
                return;
            }

            var contentStart = document.ContentStart;

            var sw = Stopwatch.StartNew();
            var root = Parser.ParseText(text);
            var elapsed = sw.Elapsed;
            sw = Stopwatch.StartNew();
            ClassifierVisitor.Visit(root, 0, text.Length, (start, length, node, classification) =>
            {
                var brush = brushes[(int)classification];

                var inline = new Run(text.Substring(start, length));
                if (brush != null)
                {
                    inline.Foreground = brush;
                }

                paragraph.Inlines.Add(inline);
            });
            elapsed = sw.Elapsed;
        }

        public static bool LooksLikeXml(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (text.Length > 10)
            {
                if (text.StartsWith("<?xml"))
                {
                    return true;
                }
                else
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        var ch = text[i];
                        if (ch == '<')
                        {
                            for (int j = text.Length - 1; j >= 0; j--)
                            {
                                ch = text[j];

                                if (ch == '>')
                                {
                                    return true;
                                }
                                else if (!char.IsWhiteSpace(ch))
                                {
                                    return false;
                                }
                            }

                            return false;
                        }
                        else if (!char.IsWhiteSpace(ch))
                        {
                            return false;
                        }
                    }
                }
            }

            return false;
        }
    }
}
