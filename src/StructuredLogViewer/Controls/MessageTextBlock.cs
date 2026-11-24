using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public class MessageTextBlock : TextBlock
    {
        public static readonly DependencyProperty MessageTextProperty = DependencyProperty.Register(
            nameof(MessageText), typeof(string), typeof(MessageTextBlock),
            new PropertyMetadata(null, (d, e) => ((MessageTextBlock)d).UpdateLinks()));

        public string MessageText
        {
            get => (string)GetValue(MessageTextProperty);
            set => SetValue(MessageTextProperty, value);
        }

        public MessageTextBlock()
        {
            Loaded += (_, _) => UpdateLinks();
        }

        private void UpdateLinks()
        {
            var text = MessageText;
            var displayedText = TextUtilities.ShortenValue(text) ?? "";
            Inlines.Clear();
            Text = displayedText;

            var navigation = this.FindAncestor<BuildControl>()?.Navigation;
            if (navigation == null)
            {
                return;
            }

            var offset = 0;
            foreach (var link in SourceFileLink.Find(text, navigation.SourceFileResolver, TextUtilities.GetShortenLength(text)))
            {
                if (offset == 0)
                {
                    Inlines.Clear();
                }

                Inlines.Add(new Run(displayedText.Substring(offset, link.Start - offset)));
                var hyperlink = new Hyperlink(new Run(link.Text))
                {
                    ToolTip = link.Text,
                    Focusable = false
                };
                hyperlink.Click += (_, e) =>
                {
                    navigation.OpenFile(link.FilePath, link.LineNumber, link.ColumnNumber);
                    e.Handled = true;
                };
                Inlines.Add(hyperlink);
                offset = link.Start + link.Length;
            }

            if (offset > 0)
            {
                Inlines.Add(new Run(displayedText.Substring(offset)));
            }
        }
    }
}
