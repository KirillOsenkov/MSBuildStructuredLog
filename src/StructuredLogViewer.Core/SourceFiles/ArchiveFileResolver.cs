using System;
using System.Collections.Generic;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class ArchiveFileResolver : ISourceFileResolver
    {
        private readonly Dictionary<string, SourceText> fileContents = new Dictionary<string, SourceText>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, SourceText> Files => fileContents;

        public ArchiveFileResolver(IEnumerable<ArchiveFile> files)
        {
            foreach (var file in files)
            {
                AddFile(file.FullPath, file.Text);
            }
        }

        public IEnumerable<string> FindFileNames(string substring)
        {
            foreach (var file in Files)
            {
                if (file.Key.IndexOf(substring, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    yield return file.Key;
                }
            }
        }

        public SourceText GetSourceFileText(string filePath)
        {
            filePath = ArchiveFile.CalculateArchivePath(filePath);
            fileContents.TryGetValue(filePath, out SourceText result);
            return result;
        }

        private void AddFile(string fullName, string text)
        {
            fileContents[fullName] = new SourceText(text);
        }
    }
}
