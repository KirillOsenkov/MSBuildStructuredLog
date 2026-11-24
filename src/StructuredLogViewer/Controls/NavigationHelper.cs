using System;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Controls
{
    public class NavigationHelper
    {
        public Build Build { get; }
        public SourceFileResolver SourceFileResolver { get; }

        public event Action<string, int, int> OpenFileRequested;

        public NavigationHelper(Build build, SourceFileResolver sourceFileResolver)
        {
            Build = build;
            SourceFileResolver = sourceFileResolver;
        }

        public void OpenFile(string filePath, int lineNumber = 0, int column = 0)
        {
            OpenFileRequested?.Invoke(filePath, lineNumber, column);
        }
    }
}
