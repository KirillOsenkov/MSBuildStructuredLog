using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        public void DisplaySource(string sourceFilePath, string text)
        {
            this.FilePath = sourceFilePath;
            this.Text = text;

            filePathText.Text = sourceFilePath;

            textEditor.Text = text;

            if (!File.Exists(FilePath))
            {
                filePathToolbar.Visibility = Visibility.Collapsed;
            }

            if (Classifier.LooksLikeXml(text))
            {
                textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");

                var foldingManager = FoldingManager.Install(textEditor.TextArea);
                var foldingStrategy = new XmlFoldingStrategy();
                foldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
            }
            //int lineCount = text.GetLineLengths().Length;
            //lineNumbers.Text = string.Join("\n", Enumerable.Range(1, lineCount).Select(i => i.ToString()));

            //var document = textBlock.Document;
            //document.PageWidth = 20000;
            //document.LineHeight = 18;
            //document.Blocks.Clear();

            //new Classifier().Classify(textBlock, text);
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
