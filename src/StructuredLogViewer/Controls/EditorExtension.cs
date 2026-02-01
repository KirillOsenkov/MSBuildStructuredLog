using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer
{
    public class EditorExtension
    {
        public PreprocessedFileManager.PreprocessContext PreprocessContext { get; set; }
        public ProjectEvaluation Evaluation { get; set; }

        public event Action<Import> ImportSelected;
        public event Action<string> GoToProperty;

        private ToolTip toolTip;
        private TextViewerControl textViewerControl;

        public void Install(TextViewerControl textViewerControl)
        {
            this.textViewerControl = textViewerControl;

            textViewerControl.EditorExtension = this;
            var textEditor = textViewerControl.TextEditor;
            var textArea = textEditor.TextArea;
            var caret = textArea.Caret;
            caret.PositionChanged += (s, e) =>
            {
                CloseToolTip();

                var caretOffset = textEditor.CaretOffset;
                if (PreprocessContext != null)
                {
                    var projectImport = PreprocessContext.GetImportFromPosition(caretOffset);
                    ImportSelected?.Invoke(projectImport);
                }

                string currentProperty = null;
                if (TryGetWordAtPosition(
                    textViewerControl,
                    caretOffset,
                    out int start,
                    out int end,
                    out string type,
                    out string word))
                {
                    if (type == "<PropertyGroup>")
                    {
                        if (start > 2)
                        {
                            var document = textViewerControl.TextEditor.Document;
                            string prefix = document.GetText(start - 2, 2);
                            if (prefix == "$(" || prefix[1] == '<')
                            {
                                currentProperty = word;
                            }
                        }
                    }
                }

                textViewerControl.CurrentProperty = currentProperty;
            };

            textEditor.MouseHover += (sender, e) =>
            {
                if (e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released)
                {
                    var mousePos = e.GetPosition(textEditor);
                    this.TryUpdateToolTipText(textViewerControl, mousePos);
                }
            };
        }

        private int lastQuickInfoStart;
        private int lastQuickInfoEnd;

        public void RaiseGoToProperty(string property)
        {
            GoToProperty?.Invoke(property);
        }

        private void TryUpdateToolTipText(TextViewerControl textViewerControl, Point mousePosition)
        {
            var offset = GetOffsetFromMousePosition(mousePosition);

            if (offset >= 0 && lastQuickInfoStart != -1 && lastQuickInfoEnd != -1)
            {
                if (offset >= lastQuickInfoStart && offset <= lastQuickInfoEnd)
                {
                    return;
                }
            }

            if (offset == -1 || !this.TryGetWordAtPosition(
                textViewerControl,
                offset,
                out int start,
                out int end,
                out string type,
                out string title)
                || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(title))
            {
                lastQuickInfoStart = -1;
                lastQuickInfoEnd = -1;
                CloseToolTip();
                return;
            }

            if (toolTip == null)
            {
                toolTip = new ToolTip
                {
                    Placement = PlacementMode.Relative,
                    PlacementTarget = textViewerControl.TextEditor.TextArea.TextView,
                    StaysOpen = true
                };
            }

            lastQuickInfoStart = start;
            lastQuickInfoEnd = end;

            var contentText = GetToolTipText(type, title);

            if (string.IsNullOrEmpty(contentText))
            {
                CloseToolTip();
                return;
            }

            var textEditor = textViewerControl.TextEditor;
            var textArea = textEditor.TextArea;
            var textView = textArea.TextView;
            var startLocation = textEditor.Document.GetLocation(start);
            var point = textView.GetVisualPosition(
                new TextViewPosition(startLocation), VisualYPosition.LineBottom);
            toolTip.HorizontalOffset = point.X - textView.ScrollOffset.X;
            toolTip.VerticalOffset = point.Y - textView.ScrollOffset.Y;
            toolTip.Content = contentText;

            if (!toolTip.IsOpen)
            {
                toolTip.IsOpen = true;
            }
        }

        private void CloseToolTip()
        {
            if (toolTip != null)
            {
                toolTip.IsOpen = false;
            }
        }

        private string GetToolTipText(string type, string title)
        {
            var evaluation = Evaluation;
            if (evaluation == null)
            {
                return null;
            }

            StringBuilder content = new();

            if (type == "<PropertyGroup>")
            {
                var propertyGroup = evaluation.Children.FirstOrDefault(p => p.Title == Strings.Properties);
                if (propertyGroup is Folder propertyFolder)
                {
                    var propertyEntry = propertyFolder.Children.FirstOrDefault(p => p.Title == title);
                    if (propertyEntry is Property property)
                    {
                        content.Append($"{title} =\n{property.Value.NormalizePropertyValue()}");
                    }
                }

                // Search the "Property reassignment" folder.
                var prFolder = evaluation.PropertyReassignmentFolder;
                if (prFolder is TimedNode folder)
                {
                    var entryFolder = folder.Children.FirstOrDefault(p => p.Title == title);
                    if (entryFolder is Folder prEntries)
                    {
                        int count = 1;
                        foreach (var entry in prEntries.Children)
                        {
                            if (count == 1)
                            {
                                content.Append("\nPrevious values:");
                            }

                            var match = Strings.PropertyReassignmentRegex.Match(entry.Title);
                            if (match.Success)
                            {
                                string value;

                                // Print the original value on first pass.
                                if (count == 1)
                                {
                                    value = match.Groups["OldValue"].Value;
                                    value = value.NormalizePropertyValue();
                                    content.Append($"\n{count}: " + value);
                                    count++;
                                }

                                value = match.Groups["NewValue"].Value;
                                value = value.NormalizePropertyValue();
                                content.Append($"\n{count}: " + value);
                                count++;
                            }
                        }
                    }
                }

                var assignmentFolder = evaluation.PropertyAssignmentFolder;
                if (assignmentFolder is TimedNode)
                {
                    var entryFolder = assignmentFolder.Children.FirstOrDefault(p => p.Title == title);
                    if (entryFolder is Folder prEntries)
                    {
                        foreach (var entry in prEntries.Children)
                        {
                            var match = Strings.PropertyAssignmentRegex.Match(entry.Title);
                            if (match.Success)
                            {
                                string value;

                                value = match.Groups["NewValue"].Value;
                                value = value.NormalizePropertyValue();
                                content.Append($"\nInitial value: " + value);
                            }
                        }
                    }
                }
            }

            var contentText = content.ToString();
            return contentText;
        }

        private int GetOffsetFromMousePosition(Point mousePosition)
        {
            var textPos = textViewerControl.TextEditor.GetPositionFromPoint(mousePosition);
            int offset = -1;
            if (textPos.HasValue)
            {
                var position = textPos.Value;
                var document = textViewerControl.TextEditor.Document;
                offset = document.GetOffset(position.Line, position.Column);
            }

            return offset;
        }

        private bool TryGetWordAtPosition(
            TextViewerControl textViewerControl,
            int offset,
            out int start,
            out int end,
            out string type,
            out string word)
        {
            type = string.Empty;
            word = string.Empty;
            start = -1;
            end = -1;

            var document = textViewerControl.TextEditor.Document;
            start = ICSharpCode.AvalonEdit.Document.TextUtilities.GetNextCaretPosition(
                document,
                offset + 1,
                System.Windows.Documents.LogicalDirection.Backward,
                CaretPositioningMode.WordBorder);
            end = ICSharpCode.AvalonEdit.Document.TextUtilities.GetNextCaretPosition(
                document,
                offset,
                System.Windows.Documents.LogicalDirection.Forward,
                CaretPositioningMode.WordBorder);

            if (start == -1 || end == -1 || end <= start)
            {
                return false;
            }

            // Try parsing the word by looking for the special characters before the word.
            if (start > 2)
            {
                // naively check for $( or <.
                word = document.GetText(start - 2, 2);
                if (word == "$(" || word[1] == '<')
                {
                    word = document.GetText(start, end - start).Trim();
                    type = "<PropertyGroup>";
                    return true;
                }
            }

            // Use the folding control to extract the containing type.
            var typeCandidate = textViewerControl.FoldingManager.GetFoldingsContaining(start)?.LastOrDefault(f => allowedFoldingNodes.Contains(f.Title));

            if (string.IsNullOrEmpty(typeCandidate?.Title))
            {
                return false;
            }

            word = document.GetText(start, end - start).Trim(specialCharacters).Trim();
            type = typeCandidate.Title;

            if (string.IsNullOrEmpty(word))
            {
                return false;
            }

            return true;
        }

        private char[] specialCharacters = ['@', '$', '(', ')', '<', '>', '\r', '\n', ' '];

        private string[] allowedFoldingNodes = [
            "<Project>",
            "<PropertyGroup>",
            "<ItemGroup>",
            ];
    }
}
