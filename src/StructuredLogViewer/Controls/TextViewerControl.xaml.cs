using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Search;

namespace StructuredLogViewer.Controls
{
    public partial class TextViewerControl : UserControl
    {
        private static readonly Regex solutionFileRegex = new Regex(@"^\s*Microsoft Visual Studio Solution File", RegexOptions.Compiled | RegexOptions.Singleline);
        private FoldingManager foldingManager;

        public string FilePath { get; private set; }
        public string Text { get; private set; }
        public Action Preprocess { get; private set; }
        public bool IsXml { get; private set; }

        public TextViewerControl()
        {
            InitializeComponent();

            var textArea = textEditor.TextArea;

            SearchPanel.Install(textArea);

            foldingManager = FoldingManager.Install(textEditor.TextArea);

            textArea.MouseRightButtonDown += TextAreaMouseRightButtonDown;
            DataObject.AddSettingDataHandler(textArea, OnSettingData);

            var textView = textArea.TextView;
            textView.Options.HighlightCurrentLine = true;
            textView.Options.EnableEmailHyperlinks = false;
            textView.Options.EnableHyperlinks = false;
            textEditor.IsReadOnly = true;

            if (SettingsService.UseDarkTheme)
            {
                textEditor.Background = ThemeManager.BackgroundBrush;
                textEditor.Foreground = ThemeManager.ControlTextBrush;
                textView.CurrentLineBackground = (Brush)new BrushConverter().ConvertFromString("#505050");
                textArea.SelectionBrush = (Brush)new BrushConverter().ConvertFromString("#264F78");
                textArea.SelectionForeground = (Brush)new BrushConverter().ConvertFromString("#C8C8C8");
                var foldingMargin = textArea.LeftMargins.OfType<FoldingMargin>().First();
                foldingMargin.FoldingMarkerBackgroundBrush = ThemeManager.BackgroundBrush;
                foldingMargin.FoldingMarkerBrush = ThemeManager.ControlTextBrush;
                foldingMargin.SelectedFoldingMarkerBackgroundBrush = ThemeManager.BackgroundBrush;
                foldingMargin.SelectedFoldingMarkerBrush = ThemeManager.ControlTextBrush;
            }
            else
            {
                textView.CurrentLineBackground = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                textView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
            }

            textEditor.ApplyTemplate();

            var scrollViewer = textEditor.FindVisualChild<ScrollViewer>();
            if (scrollViewer != null)
            {
                textEditor.PreviewMouseWheel += (s, e) =>
                {
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                        e.Handled = true;
                    }
                };
            }
        }

        private void OnSettingData(object sender, DataObjectSettingDataEventArgs e)
        {
            // disable copying HTML
            if (e.Format == DataFormats.Html || e.Format == typeof(string).FullName)
            {
                e.CancelCommand();
            }
        }

        public void DisplaySource(
            string sourceFilePath,
            string text,
            int lineNumber = 0,
            int column = 0,
            Action showPreprocessed = null,
            NavigationHelper navigationHelper = null)
        {
            this.FilePath = sourceFilePath;
            this.Preprocess = showPreprocessed;

            preprocess.Visibility = showPreprocessed != null ? Visibility.Visible : Visibility.Collapsed;

            filePathText.Text = sourceFilePath;

            SetText(text);
            DisplaySource(lineNumber, column);

            if (IsXml)
                ImportLinkHighlighter.Install(textEditor, sourceFilePath, navigationHelper);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            // Mark Ctrl+F handle to not steal focus from search panel
            if (e.Key == Key.F  && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
            }

            base.OnKeyUp(e);
        }

        private void TextAreaMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = textEditor.GetPositionFromPoint(e.GetPosition(textEditor));
            if (position.HasValue)
            {
                textEditor.TextArea.Caret.Position = position.Value;
            }
        }

        public void SetText(string text)
        {
            Text = text;
            textEditor.Text = text;

            if (text.Length > 200 && !text.Contains("\n"))
            {
                wordWrap.IsChecked = true;
            }

            if (solutionFileRegex.IsMatch(text))
            {
                IsXml = false;

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("StructuredLogViewer.Resources.SolutionFile.xshd"))
                using (var reader = XmlReader.Create(stream))
                {
                    textEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }

                return;
            }

            bool looksLikeXml = Utilities.LooksLikeXml(text);
            if (looksLikeXml && !IsXml)
            {
                IsXml = true;

                var highlighting = HighlightingManager.Instance.GetDefinition("XML");
                if (SettingsService.UseDarkTheme)
                {
                    SetColor("Comment", "#57A64A");
                    SetColor("CData", "#E9D585");
                    SetColor("DocType", "#92CAF4");
                    SetColor("XmlDeclaration", "#92CAF4");
                    SetColor("XmlTag", "#569CD6");
                    SetColor("AttributeName", "#92CAF4");
                    SetColor("AttributeValue", "#C8C8C8");
                    SetColor("Entity", "#92CAF4");
                    SetColor("BrokenEntity", "#92CAF4");
                }
                else
                {
                    SetColor("Comment", "#008000");
                    SetColor("CData", "#808080");
                    SetColor("DocType", "#0000FF");
                    SetColor("XmlDeclaration", "#0000FF");
                    SetColorRgb("XmlTag", 163, 21, 21);
                    SetColor("AttributeName", "#FF0000");
                    SetColor("AttributeValue", "#0000FF");
                    SetColor("Entity", "#FF0000");
                    SetColor("BrokenEntity", "#FF0000");
                }

                void SetColorRgb(string name, byte r, byte g, byte b)
                {
                    highlighting.GetNamedColor(name).Foreground = new SimpleHighlightingBrush(Color.FromRgb(r, g, b));
                }

                void SetColor(string name, string hex)
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    SetColorRgb(name, color.R, color.G, color.B);
                }

                textEditor.SyntaxHighlighting = highlighting;

                var foldingStrategy = new XmlFoldingStrategy();
                foldingStrategy.UpdateFoldings(foldingManager, textEditor.Document);
            }
            else if (!looksLikeXml && IsXml)
            {
                IsXml = false;

                textEditor.SyntaxHighlighting = null;
            }
        }

        public void SetPathDisplay(bool displayPath)
        {
            var visibility = displayPath ? Visibility.Visible : Visibility.Collapsed;
            this.copyFullPath.Visibility = visibility;
            this.filePathText.Visibility = visibility;
        }

        public void DisplaySource(int lineNumber, int column)
        {
            if (lineNumber > 0)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    textEditor.ScrollToLine(lineNumber);
                    textEditor.TextArea.Caret.Line = lineNumber;
                    textEditor.TextArea.TextView.HighlightedLine = lineNumber;

                    if (column > 0)
                    {
                        textEditor.ScrollTo(lineNumber, column);
                        textEditor.TextArea.Caret.Column = column;
                    }
                }, DispatcherPriority.Background);
            }
        }

        private void openInExternalEditor_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var filePath = FilePath;
            if (!File.Exists(filePath))
            {
                var extension = IsXml ? ".xml" : ".txt";
                filePath = SettingsService.WriteContentToTempFileAndGetPath(Text, extension);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                Verb = "open"
            });
        }

        private void copyFullPath_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Clipboard.SetText(FilePath);
        }

        private void preprocess_Click(object sender, RoutedEventArgs e)
        {
            Preprocess?.Invoke();
        }

        private void wordWrap_Checked(object sender, RoutedEventArgs e)
        {
            textEditor.WordWrap = true;
        }

        private void wordWrap_Unchecked(object sender, RoutedEventArgs e)
        {
            textEditor.WordWrap = false;
        }

        private void copyMenu_Click(object sender, RoutedEventArgs e)
        {
            textEditor.Copy();
        }
    }
}
