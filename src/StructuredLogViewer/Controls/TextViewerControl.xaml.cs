using System.Linq;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;

namespace StructuredLogViewer.Controls
{
    public partial class TextViewerControl : UserControl
    {
        public TextViewerControl()
        {
            InitializeComponent();
            SearchPanel.Install(textEditor.TextArea);
        }

        public void DisplaySource(string sourceFilePath, string text)
        {
            textEditor.Text = text;

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
    }
}
