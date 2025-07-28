using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;
using StructuredLogger;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace StructuredLogViewer.Controls
{

    /// <summary>
    /// Interaction logic for CommandLineDiffControl.xaml
    /// </summary>
    public partial class CommandLineDiffControl : UserControl
    {
        private string CompareButtonText = "Click to compare.";
        private string originalCommandLine = "";
        private CommandLineDiffer.CommandLineDiffSetting setting = new();

        public CommandLineDiffControl()
        {
            InitializeComponent();

            caseSensitiveCheckBox.IsChecked = true;
            wordWrap.IsChecked = false;

            SetTextEditorTheme(this.textEditorLeft);
            SetTextEditorTheme(this.textEditorRight);
            SetTextEditorTheme(this.textEditorResult);
        }

        private void SetTextEditorTheme(TextEditor textEditor)
        {
            var textArea = textEditor.TextArea;
            SearchPanel.Install(textArea);

            var textView = textArea.TextView;
            textView.Options.HighlightCurrentLine = true;
            textView.Options.EnableEmailHyperlinks = false;
            textView.Options.EnableHyperlinks = false;

            if (SettingsService.UseDarkTheme)
            {
                textEditor.Background = ThemeManager.BackgroundBrush;
                textEditor.Foreground = ThemeManager.ControlTextBrush;
                textView.CurrentLineBackground = (Brush)new BrushConverter().ConvertFromString("#505050");
                textArea.SelectionBrush = (Brush)new BrushConverter().ConvertFromString("#264F78");
                textArea.SelectionForeground = (Brush)new BrushConverter().ConvertFromString("#C8C8C8");
            }
            else
            {
                textView.CurrentLineBackground = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                textView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
            }

            textEditor.ApplyTemplate();
        }

        public void Initialize(string cmdLine)
        {

            originalCommandLine = cmdLine;
            if (StructuredLogger.CommandLineDiffer.TryParseCommandLine(cmdLine, out var parameters))
            {
                StringBuilder sbWithNewLine = new StringBuilder();
                foreach (var parameter in parameters)
                {
                    sbWithNewLine.AppendLine(parameter);
                }

                textEditorLeft.Text = sbWithNewLine.ToString();
            }
            else
            {
                textEditorLeft.Text = cmdLine;
            }
        }

        private void CompareNow_Click(object sender, RoutedEventArgs e)
        {
            setting.CaseSensitive = caseSensitiveCheckBox.IsChecked == true;
            string left = textEditorLeft.Text;
            string right = textEditorRight.Text;

            CommandLineDiffer.TryCompare(left, right, out var leftRemainder, out var rightRemainder, setting);

            string leftCombined = left.Replace(Environment.NewLine, " ");
            string rightCombined = right.Replace(Environment.NewLine, " ");
            int indexOfDiff = CommandLineDiffer.GetIndexOfFirstDifference(leftCombined, rightCombined, setting.CaseSensitive);
            StringBuilder sb = new StringBuilder();

            if (leftRemainder.Count + rightRemainder.Count == 0 && indexOfDiff == -1)
            {
                sb.Append("No differences found.");
            }
            else
            {
                if (indexOfDiff >= 0)
                {
                    // AvalonEdit.TextEditor will still wrap after 10,000 characters even when "WordWrap" is false.
                    // As a workaround, limit the number of characters printed.

                    // Print -20 and +100 characters from the indexOfDiff
                    const int prefixChars = 20;
                    const int suffixChars = 100;
                    int startIndex = indexOfDiff - prefixChars;
                    if (startIndex < 0) { startIndex = 0; }

                    int endIndexLeft = indexOfDiff + suffixChars;
                    if (endIndexLeft > leftCombined.Length) { endIndexLeft = leftCombined.Length; }

                    int endIndexRight = indexOfDiff + suffixChars;
                    if (endIndexRight > rightCombined.Length) { endIndexRight = rightCombined.Length; }

                    int lineIndexOfDiff = CommandLineDiffer.GetIndexOfFirstDifference(left, right, setting.CaseSensitive);
                    int lineOfDiffLeft = CommandLineDiffer.CountOccurrences(left, Environment.NewLine, lineIndexOfDiff);
                    int lineOfDiffRight = CommandLineDiffer.CountOccurrences(right, Environment.NewLine, lineIndexOfDiff);

                    sb.AppendLine($"Text difference start at line {lineOfDiffRight + 1}:");
                    sb.AppendLine(endIndexLeft == -1 ? "" : leftCombined.Substring(startIndex, endIndexLeft - startIndex));
                    sb.AppendLine(endIndexRight == -1 ? "" : rightCombined.Substring(startIndex, endIndexRight - startIndex));
                    sb.Append(new String('-', indexOfDiff - startIndex));
                    sb.AppendLine("^\n");
                }


                if (leftRemainder.Count + rightRemainder.Count == 0)
                {
                    sb.Append("Command Line Analyzer found no differences.");
                }
                else
                {
                    sb.AppendLine("Command Line Analyzer found differences:");
                    sb.AppendLine("Top:");

                    foreach (var line in leftRemainder)
                    {
                        sb.AppendLine(line);
                    }

                    sb.AppendLine("\nBottom:");

                    foreach (var line in rightRemainder)
                    {
                        sb.AppendLine(line);
                    }
                }
            }

            textEditorResult.Text = sb.ToString();
            CompareButton.Content = CompareButtonText;
        }

        private void copyMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.Parent is ContextMenu contextMenu &&
                contextMenu.PlacementTarget is TextEditor textEditor)
            {
                textEditor.Copy();
            }
        }

        private void textEditorLeft_TextChanged(object sender, EventArgs e)
        {
            // Any text changed, update the "Compare" button.
            CompareButton.Content = $"{CompareButtonText} (edited)";
        }

        private void wordWrap_Checked(object sender, RoutedEventArgs e)
        {
            textEditorLeft.WordWrap = true;
            textEditorRight.WordWrap = true;
        }

        private void wordWrap_Unchecked(object sender, RoutedEventArgs e)
        {
            textEditorLeft.WordWrap = false;
            textEditorRight.WordWrap = false;
        }
    }
}
