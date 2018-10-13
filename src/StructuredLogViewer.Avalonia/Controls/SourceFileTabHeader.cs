using System;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class SourceFileTabHeader
    {
        private readonly SourceFileTab tab;

        public SourceFileTabHeader(SourceFileTab sourceFileTab)
        {
            this.tab = sourceFileTab;
            Close = new Command(InvokeClose);
            Header = Path.GetFileName(tab.FilePath);
        }

        public string Header { get; private set; }

        public Command Close { get; }
        public event Action<SourceFileTab> CloseRequested;
        public void InvokeClose() => CloseRequested?.Invoke(tab);
    }
}
