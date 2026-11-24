using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class MessageTextBlock : TextBlock
    {
        protected override Type StyleKeyOverride => typeof(TextBlock);

        public static readonly StyledProperty<string> MessageTextProperty =
            AvaloniaProperty.Register<MessageTextBlock, string>(nameof(MessageText));

        public string MessageText
        {
            get => GetValue(MessageTextProperty);
            set => SetValue(MessageTextProperty, value);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateLinks();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == MessageTextProperty)
            {
                UpdateLinks();
            }
        }

        private void UpdateLinks()
        {
            var text = MessageText;
            var displayedText = TextUtilities.ShortenValue(text) ?? "";
            Inlines?.Clear();
            Text = displayedText;

            var navigation = this.FindAncestorOfType<BuildControl>()?.Navigation;
            if (navigation == null)
            {
                return;
            }

            var offset = 0;
            foreach (var link in SourceFileLink.Find(text, navigation.SourceFileResolver, TextUtilities.GetShortenLength(text)))
            {
                if (offset == 0)
                {
                    Text = null;
                }

                Inlines.Add(new Run(displayedText.Substring(offset, link.Start - offset)));
                var hyperlink = new TextBlock
                {
                    Text = link.Text,
                    TextDecorations = global::Avalonia.Media.TextDecorations.Underline,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                hyperlink.Bind(ForegroundProperty, hyperlink.GetResourceObservable("Theme_Hyperlink"));
                ToolTip.SetTip(hyperlink, link.Text);
                hyperlink.PointerReleased += (_, e) =>
                {
                    if (e.InitialPressMouseButton == MouseButton.Left)
                    {
                        navigation.OpenFile(link.FilePath, link.LineNumber, link.ColumnNumber);
                        e.Handled = true;
                    }
                };
                Inlines.Add(new InlineUIContainer(hyperlink) { BaselineAlignment = BaselineAlignment.TextBottom });
                offset = link.Start + link.Length;
            }

            if (offset > 0)
            {
                Inlines.Add(new Run(displayedText.Substring(offset)));
            }
        }
    }
}
