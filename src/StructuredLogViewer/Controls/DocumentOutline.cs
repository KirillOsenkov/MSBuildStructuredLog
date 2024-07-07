using System;
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
        }
    }
}