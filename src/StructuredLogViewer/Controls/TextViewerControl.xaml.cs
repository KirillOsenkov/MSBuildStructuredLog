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

        public TextViewerControl()
        {
            InitializeComponent();
            SearchPanel.Install(textEditor.TextArea);
        }

        public void DisplaySource(string sourceFilePath, string text, int lineNumber = 0, int column = 0)
        {
            this.FilePath = sourceFilePath;
            this.Text = text;

            filePathText.Text = sourceFilePath;

            var textView = textEditor.TextArea.TextView;
            textView.CurrentLineBackground = Brushes.LightCyan;
            textView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
            textView.Options.HighlightCurrentLine = true;
            textEditor.IsReadOnly = true;

            textEditor.Text = text;

            if (!File.Exists(FilePath))
            {
                filePathToolbar.Visibility = Visibility.Collapsed;
            }

            if (Classifier.LooksLikeXml(text))
            {
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
            Process.Start(FilePath, null);
        }

        private void copyFullPath_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Clipboard.SetText(FilePath);
        }
    }
}
