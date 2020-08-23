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

            var textView = textArea.TextView;
            textView.CurrentLineBackground = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            textView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
            textView.Options.HighlightCurrentLine = true;
            textView.Options.EnableEmailHyperlinks = false;
            textView.Options.EnableHyperlinks = false;
            textEditor.IsReadOnly = true;

            if (SettingsService.UseDarkTheme)
            {
                textEditor.Background = ThemeManager.BackgroundBrush;
                textView.CurrentLineBackground = ThemeManager.LighterBackgroundBrush;
                var foldingMargin = textArea.LeftMargins.OfType<FoldingMargin>().First();
                foldingMargin.FoldingMarkerBackgroundBrush = ThemeManager.BackgroundBrush;
                foldingMargin.FoldingMarkerBrush = ThemeManager.ControlTextBrush;
                foldingMargin.SelectedFoldingMarkerBackgroundBrush = ThemeManager.BackgroundBrush;
                foldingMargin.SelectedFoldingMarkerBrush = ThemeManager.ControlTextBrush;
            }
        }

        public void DisplaySource(
            string sourceFilePath, 
            string text, 
            int lineNumber = 0, 
            int column = 0, 
            Action showPreprocessed = null)
        {
            this.FilePath = sourceFilePath;
            this.Preprocess = showPreprocessed;

            preprocess.Visibility = showPreprocessed != null ? Visibility.Visible : Visibility.Collapsed;

            filePathText.Text = sourceFilePath;

            SetText(text);
            DisplaySource(lineNumber, column);
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
                    highlighting.GetNamedColor("XmlTag").Foreground = new SimpleHighlightingBrush(Color.FromRgb(163, 21, 21));
                }
                else
                {
                    highlighting.GetNamedColor("XmlTag").Foreground = new SimpleHighlightingBrush(Color.FromRgb(163, 21, 21));
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
