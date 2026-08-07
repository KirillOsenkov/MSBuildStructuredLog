using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using Microsoft.Build.Logging.StructuredLogger;

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
        private MenuItem gotoProjectMenu;
        private MenuItem gotoPropertyMenu;

        public string FilePath { get; private set; }
        public FoldingManager FoldingManager { get; private set; }
        public string Text { get; private set; }
        public Action Preprocess { get; private set; }
        public bool IsXml { get; private set; }
        public TextEditor TextEditor => textEditor;
        public EditorExtension EditorExtension { get; set; }

        public TextViewerControl()
        {
            InitializeComponent();

            FoldingManager = FoldingManager.Install(textEditor.TextArea);

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

            var textArea = textEditor.TextArea;
            var textView = textArea.TextView;
            textView.Options.HighlightCurrentLine = true;
            textView.Options.EnableEmailHyperlinks = false;
            textView.Options.EnableHyperlinks = false;
            textEditor.IsReadOnly = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Avalonia computes taller line metrics for Consolas than WPF's AvalonEdit;
                // tighten the spacing to match the WPF viewer
                textView.Options.LineHeightFactor = 0.87;
            }

            if (SettingsService.UseDarkTheme)
            {
                textEditor.Background = new SolidColorBrush(Color.Parse("#303030"));
                textEditor.Foreground = new SolidColorBrush(Color.Parse("#E0E0E0"));
                textView.CurrentLineBackground = new SolidColorBrush(Color.Parse("#505050"));
                textArea.SelectionBrush = new SolidColorBrush(Color.Parse("#264F78"));
                textArea.SelectionForeground = new SolidColorBrush(Color.Parse("#C8C8C8"));
                var foldingMargin = textArea.LeftMargins.OfType<FoldingMargin>().FirstOrDefault();
                if (foldingMargin != null)
                {
                    foldingMargin.FoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#303030"));
                    foldingMargin.FoldingMarkerBrush = new SolidColorBrush(Color.Parse("#E0E0E0"));
                    foldingMargin.SelectedFoldingMarkerBackgroundBrush = new SolidColorBrush(Color.Parse("#303030"));
                    foldingMargin.SelectedFoldingMarkerBrush = new SolidColorBrush(Color.Parse("#E0E0E0"));
                }
            }
            else
            {
                textView.CurrentLineBackground = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                textView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
                textArea.SelectionBrush = new SolidColorBrush(Color.Parse("#4CA0E3"));
            }
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
            this.RegisterControl(out gotoProjectMenu, nameof(gotoProjectMenu));
            this.RegisterControl(out gotoPropertyMenu, nameof(gotoPropertyMenu));

            openInExternalEditor.Click += openInExternalEditor_Click;
            copyFullPath.Click += copyFullPath_Click;
            preprocess.Click += preprocess_Click;
            wordWrap.PropertyChanged += wordWrap_Checked;
            copyMenu.Click += copyMenu_Click;
            gotoProjectMenu.Click += gotoProjectFoldingMenu_Click;
            gotoPropertyMenu.Click += gotoPropertyFoldingMenu_Click;
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

            if (TryParseCondition(text, out string newText))
            {
                textEditor.Text = newText;
                return;
            }

            bool looksLikeXml = Utilities.LooksLikeXml(text);
            if (looksLikeXml)
            {
                if (!IsXml)
                {
                    IsXml = true;

                    var highlighting = HighlightingManager.Instance.GetDefinition("XML");

                    void SetColor(string name, string hex)
                    {
                        highlighting.GetNamedColor(name).Foreground = new SimpleHighlightingBrush(Color.Parse(hex));
                    }

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
                        SetColor("XmlTag", "#A31515");
                        SetColor("AttributeName", "#FF0000");
                        SetColor("AttributeValue", "#0000FF");
                        SetColor("Entity", "#FF0000");
                        SetColor("BrokenEntity", "#FF0000");
                    }

                    textEditor.SyntaxHighlighting = highlighting;
                }

                var foldingStrategy = new XmlFoldingStrategy();
                foldingStrategy.UpdateFoldings(FoldingManager, textEditor.Document);

                gotoProjectMenu.IsVisible = true;
            }
            else if (!looksLikeXml && IsXml)
            {
                IsXml = false;

                textEditor.SyntaxHighlighting = null;
                FoldingManager.Clear();
            }
        }

        private bool TryParseCondition(string text, out string newText)
        {
            Match matches = Strings.TargetSkippedFalseConditionRegex.Match(text);
            if (!matches.Success)
            {
                matches = Strings.TaskSkippedFalseConditionRegex.Match(text);
            }

            if (matches.Success)
            {
                string unevaluated = matches.Groups[2].Value;
                string evaluated = matches.Groups[3].Value;

                try
                {
                    var nodeResult = ConditionNode.ParseAndProcess(unevaluated, evaluated);
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(text);

                    bool firstPrint = true;

                    Action<ConditionNode> nodeFormat = null;

                    nodeFormat = (ConditionNode node) =>
                    {
                        if (node.Result)
                        {
                            return;
                        }

                        if (!string.IsNullOrEmpty(node.Text))
                        {
                            if (firstPrint)
                            {
                                sb.AppendLine();
                                sb.AppendLine("Condition Analyzer:");
                                firstPrint = false;
                            }

                            sb.Append("❌ "); // X marker
                            sb.AppendLine(node.Text);
                        }

                        foreach (var child in node.Children)
                        {
                            nodeFormat(child);
                        }
                    };

                    nodeFormat(nodeResult);

                    newText = sb.ToString();
                    return true;
                }
                catch { }
            }

            newText = null;
            return false;
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
                    textEditor.ScrollToLine(lineNumber);
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

            var extensionType = new FilePickerFileType($"{extension.TrimStart('.')} files")
            {
                Patterns = new[] { "*" + extension }
            };

            using var result = await topLevel.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = "Save file as...",
                DefaultExtension = extension,
                SuggestedFileName = Path.GetFileName(filePath),
                FileTypeChoices = new[] { extensionType, FilePickerFileTypes.All, FilePickerFileTypes.TextPlain }
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

        private void gotoProjectFoldingMenu_Click(object sender, RoutedEventArgs e)
        {
            var selectionStart = textEditor.SelectionStart;
            if (selectionStart > 0)
            {
                selectionStart--;
            }

            var projFolding = this.FoldingManager.GetFoldingsContaining(selectionStart)?.LastOrDefault(f => f.Title == "<Project>");
            if (projFolding != null)
            {
                textEditor.Select(projFolding.StartOffset, projFolding.Title.Length);
                textEditor.ScrollTo(textEditor.Document.GetLineByOffset(projFolding.StartOffset).LineNumber, 0);
            }
        }

        private string currentProperty;
        public string CurrentProperty
        {
            get => currentProperty;
            set
            {
                currentProperty = value;
                gotoPropertyMenu.IsVisible = !string.IsNullOrEmpty(currentProperty);
            }
        }

        private void gotoPropertyFoldingMenu_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentProperty != null)
            {
                EditorExtension?.RaiseGoToProperty(CurrentProperty);
            }
        }
    }
}
