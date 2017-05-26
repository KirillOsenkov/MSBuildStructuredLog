using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;

namespace StructuredLogViewer.Controls
{
    public partial class TextViewerControl : UserControl
    {
        public string FilePath { get; private set; }
        public string Text { get; private set; }
        public Action Preprocess { get; private set; }
        public bool IsXml { get; private set; }

        public TextViewerControl()
        {
            InitializeComponent();
            SearchPanel.Install(textEditor.TextArea);
        }

        public void DisplaySource(string sourceFilePath, string text, int lineNumber = 0, int column = 0, Action showPreprocessed = null)
        {
            this.FilePath = sourceFilePath;
            this.Text = text;
            this.Preprocess = showPreprocessed;

            preprocess.Visibility = showPreprocessed != null ? Visibility.Visible : Visibility.Collapsed;

            filePathText.Text = sourceFilePath;

            var textView = textEditor.TextArea.TextView;
            textView.CurrentLineBackground = Brushes.LightCyan;
            textView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
            textView.Options.HighlightCurrentLine = true;
            textEditor.IsReadOnly = true;

            textEditor.Text = text;

            if (Classifier.LooksLikeXml(text))
            {
                IsXml = true;

                var highlighting = HighlightingManager.Instance.GetDefinition("XML");
                highlighting.GetNamedColor("XmlTag").Foreground = new SimpleHighlightingBrush(Color.FromRgb(163, 21, 21));
                textEditor.SyntaxHighlighting = highlighting;

                var foldingManager = FoldingManager.Install(textEditor.TextArea);
                var foldingStrategy = new XmlFoldingStrategy();
                foldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
            }

            DisplaySource(lineNumber, column);
        }

        public void DisplaySource(int lineNumber, int column)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (lineNumber > 0)
                {
                    textEditor.ScrollToLine(lineNumber);
                    textEditor.TextArea.Caret.Line = lineNumber;
                    textEditor.TextArea.TextView.HighlightedLine = lineNumber;

                    if (column > 0)
                    {
                        textEditor.ScrollTo(lineNumber, column);
                        textEditor.TextArea.Caret.Column = column;
                    }
                }
            }, DispatcherPriority.Background);
        }

        private void openInExternalEditor_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var filePath = FilePath;
            if (!File.Exists(filePath))
            {
                var extension = IsXml ? ".xml" : ".txt";
                filePath = SettingsService.WriteContentToTempFileAndGetPath(Text, extension);
            }

            Process.Start(filePath, null);
        }

        private void copyFullPath_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Clipboard.SetText(FilePath);
        }

        private void preprocess_Click(object sender, RoutedEventArgs e)
        {
            Preprocess?.Invoke();
        }
    }
}
