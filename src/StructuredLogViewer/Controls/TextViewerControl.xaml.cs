using System.Linq;
using System.Windows.Controls;

namespace StructuredLogViewer.Controls
{
    public partial class TextViewerControl : UserControl
    {
        public TextViewerControl()
        {
            InitializeComponent();
        }

        public void DisplaySource(string sourceFilePath, string text)
        {
            int lineCount = text.GetLineLengths().Length;
            lineNumbers.Text = string.Join("\n", Enumerable.Range(1, lineCount).Select(i => i.ToString()));

            var document = textBlock.Document;
            document.PageWidth = 20000;
            document.LineHeight = 18;
            document.Blocks.Clear();

            new Classifier().Classify(textBlock, text);
        }
    }
}
