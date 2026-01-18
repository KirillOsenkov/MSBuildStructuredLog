using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace StructuredLogViewer.Controls
{
    /// <summary>
    /// A TextBlock-like control that renders Markdown formatting.
    /// Supports: **bold**, *italic*, `code`, code blocks with ```, headings (#, ##, etc.), and lists (*, -, numbered)
    /// </summary>
    public class MarkdownTextBlock : RichTextBox
    {
        private static readonly Regex BoldRegex = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new Regex(@"\*(.+?)\*", RegexOptions.Compiled);
        private static readonly Regex InlineCodeRegex = new Regex(@"`(.+?)`", RegexOptions.Compiled);
        private static readonly Regex CodeBlockRegex = new Regex(@"```[\w]*\n?(.*?)\n?```", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex HeadingRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex UnorderedListRegex = new Regex(@"^[\*\-\+]\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex OrderedListRegex = new Regex(@"^\d+\.\s+(.+)$", RegexOptions.Compiled);

        public static readonly DependencyProperty MarkdownTextProperty =
            DependencyProperty.Register(
                nameof(MarkdownText),
                typeof(string),
                typeof(MarkdownTextBlock),
                new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

        public string MarkdownText
        {
            get => (string)GetValue(MarkdownTextProperty);
            set => SetValue(MarkdownTextProperty, value);
        }

        public MarkdownTextBlock()
        {
            IsReadOnly = true;
            IsReadOnlyCaretVisible = false;
            BorderThickness = new Thickness(0);
            Background = Brushes.Transparent;
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownTextBlock control)
            {
                control.RenderMarkdown(e.NewValue as string ?? string.Empty);
            }
        }

        private void RenderMarkdown(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                Document = new FlowDocument();
                return;
            }

            var flowDoc = new FlowDocument();
            flowDoc.PagePadding = new Thickness(0);

            // Process the text line by line to handle different block elements
            var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool inCodeBlock = false;
            string codeBlockContent = "";
            List currentList = null;
            bool isOrderedList = false;
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // Check for code block markers
                if (line.TrimStart().StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        // End of code block - add it
                        if (!string.IsNullOrEmpty(codeBlockContent))
                        {
                            AddCodeBlock(flowDoc, codeBlockContent.TrimEnd('\n', '\r'));
                            codeBlockContent = "";
                        }
                        inCodeBlock = false;
                        currentList = null; // Close any open list
                    }
                    else
                    {
                        // Start of code block
                        inCodeBlock = true;
                        currentList = null; // Close any open list
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    // Accumulate code block content
                    codeBlockContent += line + "\n";
                }
                else
                {
                    // Check for headings
                    var headingMatch = HeadingRegex.Match(line);
                    if (headingMatch.Success)
                    {
                        currentList = null; // Close any open list
                        var level = headingMatch.Groups[1].Value.Length;
                        var headingText = headingMatch.Groups[2].Value;
                        AddHeading(flowDoc, headingText, level);
                        continue;
                    }

                    // Check for unordered list items
                    var unorderedListMatch = UnorderedListRegex.Match(line);
                    if (unorderedListMatch.Success)
                    {
                        if (currentList == null || isOrderedList)
                        {
                            currentList = new List();
                            currentList.MarkerStyle = System.Windows.TextMarkerStyle.Disc;
                            currentList.Margin = new Thickness(0, 4, 0, 4);
                            currentList.Padding = new Thickness(20, 0, 0, 0);
                            flowDoc.Blocks.Add(currentList);
                            isOrderedList = false;
                        }
                        var listItemText = unorderedListMatch.Groups[1].Value;
                        AddListItem(currentList, listItemText);
                        continue;
                    }

                    // Check for ordered list items
                    var orderedListMatch = OrderedListRegex.Match(line);
                    if (orderedListMatch.Success)
                    {
                        if (currentList == null || !isOrderedList)
                        {
                            currentList = new List();
                            currentList.MarkerStyle = System.Windows.TextMarkerStyle.Decimal;
                            currentList.Margin = new Thickness(0, 4, 0, 4);
                            currentList.Padding = new Thickness(20, 0, 0, 0);
                            flowDoc.Blocks.Add(currentList);
                            isOrderedList = true;
                        }
                        var listItemText = orderedListMatch.Groups[1].Value;
                        AddListItem(currentList, listItemText);
                        continue;
                    }

                    // Regular paragraph
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        currentList = null; // Close any open list
                        var paragraph = new Paragraph();
                        paragraph.Margin = new Thickness(0, 4, 0, 4);
                        ProcessInlineFormatting(paragraph, line);
                        flowDoc.Blocks.Add(paragraph);
                    }
                    else
                    {
                        // Empty line closes the current list
                        currentList = null;
                    }
                }
            }

            // Handle unclosed code block
            if (inCodeBlock && !string.IsNullOrEmpty(codeBlockContent))
            {
                AddCodeBlock(flowDoc, codeBlockContent.TrimEnd('\n', '\r'));
            }

            Document = flowDoc;
        }

        private void ProcessInlineFormatting(Paragraph paragraph, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var currentText = text;
            var currentIndex = 0;

            // Process inline code first (highest priority)
            var codeMatches = InlineCodeRegex.Matches(currentText);
            if (codeMatches.Count > 0)
            {
                foreach (Match match in codeMatches)
                {
                    // Add text before match
                    if (match.Index > currentIndex)
                    {
                        ProcessBoldAndItalic(paragraph, currentText.Substring(currentIndex, match.Index - currentIndex));
                    }

                    // Add inline code
                    AddInlineCode(paragraph, match.Groups[1].Value);
                    currentIndex = match.Index + match.Length;
                }

                // Add remaining text
                if (currentIndex < currentText.Length)
                {
                    ProcessBoldAndItalic(paragraph, currentText.Substring(currentIndex));
                }
            }
            else
            {
                ProcessBoldAndItalic(paragraph, currentText);
            }
        }

        private void ProcessBoldAndItalic(Paragraph paragraph, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var currentText = text;
            var currentIndex = 0;

            // Process bold
            var boldMatches = BoldRegex.Matches(currentText);
            if (boldMatches.Count > 0)
            {
                foreach (Match match in boldMatches)
                {
                    // Add text before match
                    if (match.Index > currentIndex)
                    {
                        ProcessItalic(paragraph, currentText.Substring(currentIndex, match.Index - currentIndex));
                    }

                    // Add bold text
                    var bold = new Bold(new Run(match.Groups[1].Value));
                    paragraph.Inlines.Add(bold);
                    currentIndex = match.Index + match.Length;
                }

                // Add remaining text
                if (currentIndex < currentText.Length)
                {
                    ProcessItalic(paragraph, currentText.Substring(currentIndex));
                }
            }
            else
            {
                ProcessItalic(paragraph, currentText);
            }
        }

        private void ProcessItalic(Paragraph paragraph, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var currentText = text;
            var currentIndex = 0;

            var italicMatches = ItalicRegex.Matches(currentText);
            if (italicMatches.Count > 0)
            {
                foreach (Match match in italicMatches)
                {
                    // Add text before match
                    if (match.Index > currentIndex)
                    {
                        paragraph.Inlines.Add(new Run(currentText.Substring(currentIndex, match.Index - currentIndex)));
                    }

                    // Add italic text
                    var italic = new Italic(new Run(match.Groups[1].Value));
                    paragraph.Inlines.Add(italic);
                    currentIndex = match.Index + match.Length;
                }

                // Add remaining text
                if (currentIndex < currentText.Length)
                {
                    paragraph.Inlines.Add(new Run(currentText.Substring(currentIndex)));
                }
            }
            else
            {
                paragraph.Inlines.Add(new Run(currentText));
            }
        }

        private void AddInlineCode(Paragraph paragraph, string code)
        {
            var run = new Run(code)
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 0, 0))
            };
            paragraph.Inlines.Add(run);
        }

        private void AddCodeBlock(FlowDocument flowDoc, string code)
        {
            var codeBlock = new Paragraph(new Run(code))
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 4, 0, 4),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1)
            };

            flowDoc.Blocks.Add(codeBlock);
        }

        private void AddHeading(FlowDocument flowDoc, string text, int level)
        {
            var paragraph = new Paragraph();
            paragraph.Margin = new Thickness(0, level == 1 ? 8 : 6, 0, 4);

            var run = new Run(text);
            run.FontWeight = FontWeights.Bold;

            // Set font size based on heading level
            switch (level)
            {
                case 1:
                    run.FontSize = 20;
                    break;
                case 2:
                    run.FontSize = 18;
                    break;
                case 3:
                    run.FontSize = 16;
                    break;
                case 4:
                    run.FontSize = 14;
                    break;
                case 5:
                    run.FontSize = 12;
                    break;
                case 6:
                    run.FontSize = 11;
                    break;
            }

            paragraph.Inlines.Add(run);
            flowDoc.Blocks.Add(paragraph);
        }

        private void AddListItem(List list, string text)
        {
            var listItem = new ListItem();
            var paragraph = new Paragraph();
            paragraph.Margin = new Thickness(0, 2, 0, 2);
            ProcessInlineFormatting(paragraph, text);
            listItem.Blocks.Add(paragraph);
            list.ListItems.Add(listItem);
        }
    }
}
