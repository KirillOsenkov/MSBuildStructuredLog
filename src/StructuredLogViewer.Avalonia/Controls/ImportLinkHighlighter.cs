using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Microsoft.Build.Logging.StructuredLogger;
using FontStyle = Avalonia.Media.FontStyle;

namespace StructuredLogViewer.Avalonia.Controls
{
    internal static class ImportLinkHighlighter
    {
        private const string ImportElementName = "Import";

        public static void Install(TextEditor textEditor, string filePath, NavigationHelper navigationHelper)
        {
            if (navigationHelper == null || string.IsNullOrEmpty(filePath))
                return;

            var importsByLocation = new Dictionary<TextLocation, HashSet<string>>();

            foreach (var import in navigationHelper.Build.EvaluationFolder.Children.OfType<ProjectEvaluation>().SelectMany(i => i.GetAllImportsTransitive()))
            {
                if (!string.Equals(import.ProjectFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(import.ImportedProjectFilePath))
                    continue;

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
                return;

            textEditor.TextArea.TextView.ElementGenerators.Add(new ImportLinkGenerator(textEditor.TextArea.TextView, importsByLocation, navigationHelper));
        }

        private class ImportLinkGenerator : VisualLineElementGenerator
        {

            private static readonly Cursor iBeamCursor = new(StandardCursorType.Ibeam);

            private readonly Dictionary<TextLocation, HashSet<string>> importsByLocation;
            private readonly NavigationHelper navigationHelper;

            public TextView TextView { get; }
            public bool WasCursorSetByLink;

            public ImportLinkGenerator(TextView textView, Dictionary<TextLocation, HashSet<string>> importsByLocation, NavigationHelper navigationHelper)
            {
                TextView = textView;

                this.importsByLocation = importsByLocation;
                this.navigationHelper = navigationHelper;

                textView.PointerMoved += (_, _) =>
                {
                    if (!WasCursorSetByLink)
                        return;

                    textView.Cursor = iBeamCursor;
                    WasCursorSetByLink = false;
                };
            }

            public override int GetFirstInterestedOffset(int startOffset)
            {
                var endOffset = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
                var relevantText = CurrentContext.GetText(startOffset, endOffset - startOffset);

                var index = relevantText.Text.IndexOf("<" + ImportElementName, relevantText.Offset, relevantText.Count, StringComparison.Ordinal);
                if (index < 0)
                    return -1;

                var elementStartOffset = index - relevantText.Offset + startOffset;
                return elementStartOffset + 1;
            }

            public override VisualLineElement ConstructElement(int offset)
            {
                // The offset should point to the "I" in "<Import"
                var text = CurrentContext.GetText(offset, ImportElementName.Length);
                if (text.Text.IndexOf(ImportElementName, text.Offset, text.Count, StringComparison.Ordinal) != text.Offset)
                    return null;

                var location = CurrentContext.Document.GetLocation(offset - 1);

                if (!importsByLocation.TryGetValue(location, out var importedPaths))
                    return null;

                return new ImportLinkElement(this, CurrentContext.VisualLine, text.Count, importedPaths, navigationHelper);
            }
        }

        private class ImportLinkElement : VisualLineText
        {
            private static readonly Cursor handCursor = new(StandardCursorType.Hand);

            private readonly ImportLinkGenerator generator;
            private readonly HashSet<string> importedPaths;
            private readonly NavigationHelper navigationHelper;

            public ImportLinkElement(ImportLinkGenerator generator, VisualLine parentVisualLine, int length, HashSet<string> importedPaths, NavigationHelper navigationHelper)
                : base(parentVisualLine, length)
            {
                this.generator = generator;
                this.importedPaths = importedPaths;
                this.navigationHelper = navigationHelper;
            }

            public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
            {
                TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                return base.CreateTextRun(startVisualColumn, context);
            }

            protected override void OnQueryCursor(PointerEventArgs e)
            {
                base.OnQueryCursor(e);

                // TODO Do this properly when the following issue is fixed: https://github.com/AvaloniaUI/AvaloniaEdit/issues/133

                if (!e.Handled && (e.KeyModifiers & KeyModifiers.Control) != 0 && ReferenceEquals(e.Source, generator.TextView))
                {
                    generator.TextView.Cursor = handCursor;
                    generator.WasCursorSetByLink = true;
                    e.Handled = true;
                }
            }

            protected override void OnPointerPressed(PointerPressedEventArgs e)
            {
                if (e.Handled)
                    return;

                if ((e.KeyModifiers & KeyModifiers.Control) != 0 && e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                {
                    OpenLink();
                    e.Handled = true;
                }

                base.OnPointerPressed(e);
            }

            protected override VisualLineText CreateInstance(int length)
            {
                return new ImportLinkElement(generator, ParentVisualLine, length, importedPaths, navigationHelper);
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
                    WindowManagerAddShadowHint = false,
                    FontFamily = FontFamily.Default,
                    Cursor = Cursor.Default
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

                menu.Placement = PlacementMode.AnchorAndGravity;
                menu.PlacementAnchor = PopupAnchor.BottomLeft;
                menu.PlacementGravity = PopupGravity.BottomRight;
                menu.PlacementTarget = generator.TextView;
                menu.PlacementRect = new Rect(topLeft - generator.TextView.ScrollOffset, bottomRight - generator.TextView.ScrollOffset);
                menu.PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.FlipY | PopupPositionerConstraintAdjustment.SlideX;
                menu.Open(generator.TextView);
            }

            private int GetCommonPathLength()
            {
                if (importedPaths.Count < 2)
                    return 0;

                var paths = importedPaths.Select(i => i.ToLowerInvariant()).ToList();
                var charCountToConsider = paths.Min(i => i.Length);

                var result = 0;

                for (var charIndex = 0; charIndex < charCountToConsider; ++charIndex)
                {
                    var currentChar = paths[0][charIndex];

                    for (var pathIndex = 1; pathIndex < paths.Count; ++pathIndex)
                    {
                        if (paths[pathIndex][charIndex] != currentChar)
                            return result;
                    }

                    if (currentChar is '\\' or '/')
                        result = charIndex;
                }

                return result;
            }
        }
    }
}
