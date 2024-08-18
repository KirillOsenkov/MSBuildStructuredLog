using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Avalonia;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Interactivity;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using AvaloniaEdit.Highlighting.Xshd;
using System.Text;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;

namespace StructuredLogViewer.Avalonia.Controls
{
    public partial class TextViewerControl : UserControl
    {
        private static readonly Regex solutionFileRegex = new Regex(@"^\s*Microsoft Visual Studio Solution File", RegexOptions.Compiled | RegexOptions.Singleline);

        private TextEditor textEditor;
        private Button preprocess;
        private TextBox filePathText;
        private Button copyFullPath;
        private CheckBox wordWrap;
        private Button openInExternalEditor;
        private MenuItem copyMenu;

        public string FilePath { get; private set; }
        public string Text { get; private set; }
        public Action Preprocess { get; private set; }
        public bool IsXml { get; private set; }

        public TextViewerControl()
        {
            InitializeComponent();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                textEditor.FontFamily = "Consolas";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                textEditor.FontFamily = "Menlo";
            }
            else
            {
                textEditor.FontFamily = "Monospace";
            }

            textEditor.TextArea.PointerPressed += TextAreaMouseRightButtonDown;

            var textView = textEditor.TextArea.TextView;
            textView.Options.HighlightCurrentLine = true;
            textView.Options.EnableEmailHyperlinks = false;
            textView.Options.EnableHyperlinks = false;
            textEditor.IsReadOnly = true;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.RegisterControl(out textEditor, nameof(textEditor));
            this.RegisterControl(out preprocess, nameof(preprocess));
            this.RegisterControl(out filePathText, nameof(filePathText));
            this.RegisterControl(out copyFullPath, nameof(copyFullPath));
            this.RegisterControl(out wordWrap, nameof(wordWrap));
            this.RegisterControl(out openInExternalEditor, nameof(openInExternalEditor));
            this.RegisterControl(out copyMenu, nameof(copyMenu));

            openInExternalEditor.Click += openInExternalEditor_Click;
            copyFullPath.Click += copyFullPath_Click;
            preprocess.Click += preprocess_Click;
            wordWrap.PropertyChanged += wordWrap_Checked;
            copyMenu.Click += copyMenu_Click;
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

            preprocess.IsVisible = showPreprocessed != null;

            filePathText.Text = sourceFilePath;

            SetText(text);
            DisplaySource(lineNumber, column);

            if (IsXml)
                ImportLinkHighlighter.Install(textEditor, sourceFilePath, navigationHelper);
        }

        private void TextAreaMouseRightButtonDown(object sender, PointerEventArgs e)
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

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("StructuredLogViewer.Avalonia.Resources.SolutionFile.xshd"))
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
                highlighting.GetNamedColor("XmlTag").Foreground = new SimpleHighlightingBrush(Color.FromRgb(163, 21, 21));
                textEditor.SyntaxHighlighting = highlighting;
                
                var foldingManager = FoldingManager.Install(textEditor.TextArea);
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
            this.copyFullPath.IsVisible = displayPath;
            this.filePathText.IsVisible = displayPath;
        }

        public void DisplaySource(int lineNumber, int column)
        {
            if (lineNumber > 0)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    textEditor.TextArea.Caret.Line = lineNumber;
                    textEditor.TextArea.Caret.BringCaretToView();
                    textEditor.TextArea.TextView.HighlightedLine = lineNumber;

                    if (column > 0)
                    {
                        textEditor.ScrollTo(lineNumber, column);
                        textEditor.TextArea.Caret.Column = column;
                    }
                }, DispatcherPriority.Background);
            }
        }

        private async void save_Click(object sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not TopLevel topLevel)
            {
                return;
            }
            
            var filePath = FilePath;
            var extension = Path.GetExtension(filePath);

            if (string.IsNullOrEmpty(extension))
            {
                extension = ".txt";
                filePath += extension;
            }

            using var result = await topLevel.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = "Save file as...",
                DefaultExtension = extension,
                SuggestedFileName = Path.GetFileName(filePath),
                FileTypeChoices = new[] { FilePickerFileTypes.All, FilePickerFileTypes.TextPlain }
            });

            if (result is not null)
            {
                await using var stream = await result.OpenWriteAsync();
                await using var writer = new StreamWriter(stream, Encoding.UTF8);
                await writer.WriteAsync(Text);
            }
        }

        private void openInExternalEditor_Click(object sender, RoutedEventArgs e)
        {
            var filePath = FilePath;
            if (!File.Exists(filePath))
            {
                var extension = IsXml ? ".xml" : ".txt";
                filePath = SettingsService.WriteContentToTempFileAndGetPath(Text, extension);
            }

            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        private void copyFullPath_Click(object sender, RoutedEventArgs e)
        {
            TopLevel.GetTopLevel(this).Clipboard.SetTextAsync(FilePath);
        }

        private void preprocess_Click(object sender, RoutedEventArgs e)
        {
            Preprocess?.Invoke();
        }

        private void wordWrap_Checked(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == CheckBox.IsCheckedProperty)
            {
                textEditor.WordWrap = wordWrap.IsChecked.GetValueOrDefault();
            }
        }

        private void copyMenu_Click(object sender, RoutedEventArgs e)
        {
            textEditor.Copy();
        }
    }
}
