using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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

        public static void Install(TextEditor textEditor, Build build, string filePath)
        {
            if (build == null || string.IsNullOrEmpty(filePath))
                return;

            var importsByLocation = new Dictionary<TextLocation, string>();
            var ambiguousLocations = new HashSet<TextLocation>();

            foreach (var import in build.EvaluationFolder.Children.OfType<ProjectEvaluation>().SelectMany(i => i.GetAllImports()))
            {
                if (!string.Equals(import.ProjectFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(import.ImportedProjectFilePath))
                    continue;

                var location = new TextLocation(import.Line, import.Column);

                if (importsByLocation.TryGetValue(location, out var existingImport))
                {
                    if (!string.Equals(existingImport, import.ImportedProjectFilePath, StringComparison.OrdinalIgnoreCase))
                        ambiguousLocations.Add(location);
                }
                else
                {
                    importsByLocation.Add(location, import.ImportedProjectFilePath);
                }
            }

            foreach (var location in ambiguousLocations)
                importsByLocation.Remove(location);

            if (importsByLocation.Count == 0)
                return;

            textEditor.TextArea.TextView.ElementGenerators.Add(new ImportLinkGenerator(importsByLocation));
        }

        private class ImportLinkGenerator : VisualLineElementGenerator
        {
            private readonly Dictionary<TextLocation, string> imports;

            public ImportLinkGenerator(Dictionary<TextLocation, string> imports)
            {
                this.imports = imports;
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

                if (!imports.TryGetValue(location, out var importedPath))
                    return null;

                return new ImportLinkElement(CurrentContext.VisualLine, text.Count, importedPath);
            }
        }

        private class ImportLinkElement : VisualLineText
        {
            public string ImportedPath { get; }

            public ImportLinkElement(VisualLine parentVisualLine, int length, string importedPath)
                : base(parentVisualLine, length)
            {
                ImportedPath = importedPath;
            }

            public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
            {
                TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                return base.CreateTextRun(startVisualColumn, context);
            }

            protected override VisualLineText CreateInstance(int length)
            {
                return new ImportLinkElement(ParentVisualLine, length, ImportedPath);
            }
        }
    }
}
