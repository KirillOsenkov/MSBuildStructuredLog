using Avalonia.Controls;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class SourceFileTab : TabItem
    {
        public string FilePath { get; set; }
        public string Text { get; set; }
    }
}
