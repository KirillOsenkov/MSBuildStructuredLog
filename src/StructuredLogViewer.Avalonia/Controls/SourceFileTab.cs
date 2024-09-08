using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class SourceFileTab
    {
        public string FileName => Path.GetFileName(FilePath);
        public string FilePath { get; set; }

        public TextViewerControl Content { get; set; }

        public Command Close { get; }
        public event Action<SourceFileTab> CloseRequested;

        public SourceFileTab()
        {
            Close = new Command(() => CloseRequested?.Invoke(this));
        }
    }
}
