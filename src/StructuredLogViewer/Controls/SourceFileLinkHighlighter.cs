using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.TextFormatting;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    internal class SourceFileLinkHighlighter : VisualLineElementGenerator
    {
        private readonly NavigationHelper navigation;
        private readonly TextViewerControl owner;
        private SourceFileLink currentLink;
        private int currentOffset;

        private SourceFileLinkHighlighter(TextViewerControl owner, NavigationHelper navigation)
        {
            this.owner = owner;
            this.navigation = navigation;
        }

        public static void Install(TextViewerControl owner, NavigationHelper navigation)
        {
            var editor = owner.TextEditor;
            var generators = editor.TextArea.TextView.ElementGenerators;
            foreach (var existing in generators.OfType<SourceFileLinkHighlighter>().ToArray())
            {
                generators.Remove(existing);
            }

            if (navigation != null)
            {
                generators.Add(new SourceFileLinkHighlighter(owner, navigation));
            }
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            if (owner.IsXml)
            {
                return -1;
            }

            var start = CurrentContext.VisualLine.FirstDocumentLine.Offset;
            var end = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
            var text = CurrentContext.Document.GetText(start, end - start);
            foreach (var link in SourceFileLink.Find(text, navigation.SourceFileResolver))
            {
                if (start + link.Start >= startOffset)
                {
                    currentLink = link;
                    currentOffset = start + link.Start;
                    return currentOffset;
                }
            }

            return -1;
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            return offset == currentOffset
                ? new LinkElement(CurrentContext.VisualLine, currentLink.Length, currentLink, navigation)
                : null;
        }

        private class LinkElement : VisualLineText
        {
            private readonly SourceFileLink link;
            private readonly NavigationHelper navigation;

            public LinkElement(VisualLine line, int length, SourceFileLink link, NavigationHelper navigation)
                : base(line, length)
            {
                this.link = link;
                this.navigation = navigation;
            }

            public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
            {
                TextRunProperties.SetForegroundBrush(context.TextView.LinkTextForegroundBrush);
                TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                return base.CreateTextRun(startVisualColumn, context);
            }

            protected override void OnQueryCursor(QueryCursorEventArgs e)
            {
                base.OnQueryCursor(e);
                e.Cursor = Cursors.Hand;
                e.Handled = true;
            }

            protected override void OnMouseDown(MouseButtonEventArgs e)
            {
                if (!e.Handled && e.ChangedButton == MouseButton.Left)
                {
                    navigation.OpenFile(link.FilePath, link.LineNumber, link.ColumnNumber);
                    e.Handled = true;
                }

                base.OnMouseDown(e);
            }

            protected override VisualLineText CreateInstance(int length)
            {
                return new LinkElement(ParentVisualLine, length, link, navigation);
            }
        }
    }
}
