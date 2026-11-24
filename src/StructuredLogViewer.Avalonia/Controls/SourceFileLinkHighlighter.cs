using System.Linq;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Avalonia.Controls
{
    internal class SourceFileLinkHighlighter : VisualLineElementGenerator
    {
        private static readonly Cursor handCursor = new Cursor(StandardCursorType.Hand);
        private static readonly Cursor iBeamCursor = new Cursor(StandardCursorType.Ibeam);
        private readonly NavigationHelper navigation;
        private readonly TextViewerControl owner;
        private readonly TextView textView;
        private SourceFileLink currentLink;
        private int currentOffset;
        private bool wasCursorSet;

        private SourceFileLinkHighlighter(TextViewerControl owner, NavigationHelper navigation)
        {
            this.owner = owner;
            textView = owner.TextEditor.TextArea.TextView;
            this.navigation = navigation;
            textView.PointerMoved += OnPointerMoved;
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            // AvaloniaEdit does not reset link cursors yet (AvaloniaEdit#133).
            if (wasCursorSet)
            {
                textView.Cursor = iBeamCursor;
                wasCursorSet = false;
            }
        }

        public static void Install(TextViewerControl owner, NavigationHelper navigation)
        {
            var textView = owner.TextEditor.TextArea.TextView;
            var generators = textView.ElementGenerators;
            foreach (var existing in generators.OfType<SourceFileLinkHighlighter>().ToArray())
            {
                textView.PointerMoved -= existing.OnPointerMoved;
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
                ? new LinkElement(this, CurrentContext.VisualLine, currentLink.Length, currentLink)
                : null;
        }

        private class LinkElement : VisualLineText
        {
            private readonly SourceFileLinkHighlighter generator;
            private readonly SourceFileLink link;

            public LinkElement(SourceFileLinkHighlighter generator, VisualLine line, int length, SourceFileLink link)
                : base(line, length)
            {
                this.generator = generator;
                this.link = link;
            }

            public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
            {
                TextRunProperties.SetForegroundBrush(context.TextView.LinkTextForegroundBrush);
                TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                return base.CreateTextRun(startVisualColumn, context);
            }

            protected override void OnQueryCursor(PointerEventArgs e)
            {
                base.OnQueryCursor(e);
                if (!e.Handled && ReferenceEquals(e.Source, generator.textView))
                {
                    generator.textView.Cursor = handCursor;
                    generator.wasCursorSet = true;
                    e.Handled = true;
                }
            }

            protected override void OnPointerPressed(PointerPressedEventArgs e)
            {
                if (!e.Handled && e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                {
                    generator.navigation.OpenFile(link.FilePath, link.LineNumber, link.ColumnNumber);
                    e.Handled = true;
                }

                base.OnPointerPressed(e);
            }

            protected override VisualLineText CreateInstance(int length)
            {
                return new LinkElement(generator, ParentVisualLine, length, link);
            }
        }
    }
}
