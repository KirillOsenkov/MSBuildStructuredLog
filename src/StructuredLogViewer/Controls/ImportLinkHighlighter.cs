using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.TextFormatting;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    internal static class ImportLinkHighlighter
    {
        private const string ImportElementName = "Import";

        public static void Install(TextEditor textEditor, string filePath, NavigationHelper navigationHelper)
        {
            if (navigationHelper == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var importsByLocation = new Dictionary<TextLocation, HashSet<string>>();

            foreach (var import in navigationHelper.Build.EvaluationFolder.Children.OfType<ProjectEvaluation>().SelectMany(i => i.GetAllImportsTransitive()))
            {
                if (!string.Equals(import.ProjectFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(import.ImportedProjectFilePath))
                {
                    continue;
                }

                var location = new TextLocation(import.Line, import.Column);

                if (importsByLocation.TryGetValue(location, out var existingImports))
                {
                    existingImports.Add(import.ImportedProjectFilePath);
                }
                else
                {
                    importsByLocation.Add(location, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        import.ImportedProjectFilePath
                    });
                }
            }

            if (importsByLocation.Count == 0)
            {
                return;
            }

            textEditor.TextArea.TextView.ElementGenerators.Add(new ImportLinkGenerator(importsByLocation, navigationHelper));
        }

        private class ImportLinkGenerator : VisualLineElementGenerator
        {
            private readonly Dictionary<TextLocation, HashSet<string>> importsByLocation;
            private readonly NavigationHelper navigationHelper;

            public ImportLinkGenerator(Dictionary<TextLocation, HashSet<string>> importsByLocation, NavigationHelper navigationHelper)
            {
                this.importsByLocation = importsByLocation;
                this.navigationHelper = navigationHelper;
            }

            public override int GetFirstInterestedOffset(int startOffset)
            {
                var endOffset = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
                var relevantText = CurrentContext.GetText(startOffset, endOffset - startOffset);

                var index = relevantText.Text.IndexOf("<" + ImportElementName, relevantText.Offset, relevantText.Count, StringComparison.Ordinal);
                if (index < 0)
                {
                    return -1;
                }

                var elementStartOffset = index - relevantText.Offset + startOffset;
                return elementStartOffset + 1;
            }

            public override VisualLineElement ConstructElement(int offset)
            {
                // The offset should point to the "I" in "<Import"
                var text = CurrentContext.GetText(offset, ImportElementName.Length);
                if (text.Text.IndexOf(ImportElementName, text.Offset, text.Count, StringComparison.Ordinal) != text.Offset)
                {
                    return null;
                }

                var location = CurrentContext.Document.GetLocation(offset - 1);

                if (!importsByLocation.TryGetValue(location, out var importedPaths))
                {
                    return null;
                }

                return new ImportLinkElement(CurrentContext.TextView, CurrentContext.VisualLine, text.Count, importedPaths, navigationHelper);
            }
        }

        private class ImportLinkElement : VisualLineText
        {
            private readonly TextView textView;
            private readonly HashSet<string> importedPaths;
            private readonly NavigationHelper navigationHelper;

            public ImportLinkElement(TextView textView, VisualLine parentVisualLine, int length, HashSet<string> importedPaths, NavigationHelper navigationHelper)
                : base(parentVisualLine, length)
            {
                this.textView = textView;
                this.importedPaths = importedPaths;
                this.navigationHelper = navigationHelper;
            }

            public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
            {
                TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                return base.CreateTextRun(startVisualColumn, context);
            }

            protected override void OnQueryCursor(QueryCursorEventArgs e)
            {
                base.OnQueryCursor(e);

                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    e.Handled = true;
                    e.Cursor = Cursors.Hand;
                }
            }

            protected override void OnMouseDown(MouseButtonEventArgs e)
            {
                if (!e.Handled && e.ChangedButton == MouseButton.Left && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    OpenLink();
                    e.Handled = true;
                }

                base.OnMouseDown(e);
            }

            protected override VisualLineText CreateInstance(int length)
            {
                return new ImportLinkElement(textView, ParentVisualLine, length, importedPaths, navigationHelper);
            }

            private void OpenLink()
            {
                if (importedPaths.Count == 1)
                {
                    var filePath = importedPaths.Single();

                    if (navigationHelper.SourceFileResolver.HasFile(filePath))
                    {
                        navigationHelper.OpenFile(filePath);
                        return;
                    }
                }

                OpenMenu();
            }

            private void OpenMenu()
            {
                var menu = new ContextMenu
                {
                    HasDropShadow = false
                };

                var commonPathLength = GetCommonPathLength();

                foreach (var filePath in importedPaths.OrderBy(i => i, StringComparer.OrdinalIgnoreCase))
                {
                    var menuItem = new MenuItem
                    {
                        Header = commonPathLength < 20 ? filePath : "..." + filePath.Substring(commonPathLength),
                    };

                    if (navigationHelper.SourceFileResolver.HasFile(filePath))
                    {
                        menuItem.Click += (_, _) => navigationHelper.OpenFile(filePath);
                    }
                    else
                    {
                        menuItem.IsEnabled = false;
                    }

                    menu.AddItem(menuItem);
                }

                var topLeft = ParentVisualLine.GetVisualPosition(VisualColumn, VisualYPosition.LineTop);
                var bottomRight = ParentVisualLine.GetVisualPosition(VisualColumn + VisualLength, VisualYPosition.LineBottom);

                menu.Placement = PlacementMode.Bottom;
                menu.PlacementTarget = textView;
                menu.PlacementRectangle = new Rect(topLeft - textView.ScrollOffset, bottomRight - textView.ScrollOffset);
                menu.IsOpen = true;
            }

            private int GetCommonPathLength()
            {
                if (importedPaths.Count < 2)
                {
                    return 0;
                }

                var paths = importedPaths.Select(i => i.ToLowerInvariant()).ToList();
                var charCountToConsider = paths.Min(i => i.Length);

                var result = 0;

                for (var charIndex = 0; charIndex < charCountToConsider; ++charIndex)
                {
                    var currentChar = paths[0][charIndex];

                    for (var pathIndex = 1; pathIndex < paths.Count; ++pathIndex)
                    {
                        if (paths[pathIndex][charIndex] != currentChar)
                        {
                            return result;
                        }
                    }

                    if (currentChar is '\\' or '/')
                    {
                        result = charIndex;
                    }
                }

                return result;
            }
        }
    }
}
