using System;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class NavigationHelper
    {
        public Build Build { get; }
        public SourceFileResolver SourceFileResolver { get; }

        public event Action<string> OpenFileRequested;

        public NavigationHelper(Build build, SourceFileResolver sourceFileResolver)
        {
            Build = build;
            SourceFileResolver = sourceFileResolver;
        }

        public void OpenFile(string filePath)
        {
            OpenFileRequested?.Invoke(filePath);
        }
    }
}
