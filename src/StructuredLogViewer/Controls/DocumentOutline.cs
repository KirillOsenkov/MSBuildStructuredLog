using System;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer
{
    public class EditorExtension
    {
        public PreprocessedFileManager.PreprocessContext PreprocessContext { get; set; }

        public event Action<Import> ImportSelected;

        public void Install(TextViewerControl textViewerControl)
        {
            textViewerControl.EditorExtension = this;
            var textEditor = textViewerControl.TextEditor;
            var textArea = textEditor.TextArea;
            var caret = textArea.Caret;
            caret.PositionChanged += (s, e) =>
            {
                var caretOffset = textEditor.CaretOffset;
                var projectImport = PreprocessContext.GetImportFromPosition(caretOffset);
                ImportSelected?.Invoke(projectImport);
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

        private void TryUpdateToolTipText(TextViewerControl textViewerControl, System.Windows.Point mousePosition)
        {
            var textPos = textViewerControl.TextEditor.GetPositionFromPoint(mousePosition);
            textViewerControl.ToolTip ??= new ToolTip() { Placement = PlacementMode.Relative, PlacementTarget = textViewerControl.TextEditor };
            ToolTip tooltip = textViewerControl.ToolTip as ToolTip;

            if (!textPos.HasValue || !this.TryGetWordAtPosition(textViewerControl, textPos.Value, out string type, out string title)
                || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(title))
            {
                CloseToolTip();
                return;
            }

            StringBuilder content = new();

            if (type == "<PropertyGroup>")
            {
                var propertyGroup = this.PreprocessContext.Evaluation.Children.FirstOrDefault(p => p.Title == Strings.Properties);
                if (propertyGroup is Folder propertyFolder)
                {
                    var propertyEntry = propertyFolder.Children.FirstOrDefault(p => p.Title == title);
                    if (propertyEntry is Property property)
                    {
                        content.Append($"{title} =\n{property.Value.NormalizePropertyValue()}");
                    }
                }

                // Search the "Property reassignment" folder.
                ;
                var prFolder = this.PreprocessContext.Evaluation.Children.FirstOrDefault(p => p.Title == Strings.PropertyReassignmentFolder);
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
            }

            if (content.Length == 0)
            {
                CloseToolTip();
                return;
            }

            tooltip.HorizontalOffset = mousePosition.X;
            tooltip.VerticalOffset = mousePosition.Y;
            tooltip.Content = content;

            if (!tooltip.IsOpen)
            {
                tooltip.IsOpen = true;
            }

            void CloseToolTip()
            {
                if (tooltip.IsOpen)
                {
                    tooltip.IsOpen = false;
                }
            }
        }

        private bool TryGetWordAtPosition(TextViewerControl textViewerControl, TextViewPosition textPos, out string type, out string word)
        {
            type = string.Empty;
            word = string.Empty;

            var document = textViewerControl.TextEditor.Document;
            var offset = document.GetOffset(textPos.Line, textPos.Column);
            int start = ICSharpCode.AvalonEdit.Document.TextUtilities.GetNextCaretPosition(document, offset + 1, System.Windows.Documents.LogicalDirection.Backward, ICSharpCode.AvalonEdit.Document.CaretPositioningMode.WordBorder);
            int end = ICSharpCode.AvalonEdit.Document.TextUtilities.GetNextCaretPosition(document, offset, System.Windows.Documents.LogicalDirection.Forward, ICSharpCode.AvalonEdit.Document.CaretPositioningMode.WordBorder);

            if (start == -1 || end == -1 || end <= start)
            {
                return false;
            }

            // Try parsing the word by looking for the special characters before the word.
            if (start > 2)
            {
                // naively check for $(.
                word = document.GetText(start - 2, 2);
                if (word == "$(")
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
