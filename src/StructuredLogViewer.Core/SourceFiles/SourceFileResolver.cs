using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class SourceFileResolver : ISourceFileResolver
    {
        private readonly List<ISourceFileResolver> resolvers = new List<ISourceFileResolver>
        {
            new LocalSourceFileResolver()
        };

        private const string buildsourceszip = ".buildsources.zip";

        private readonly Dictionary<string, SourceText> fileContentsCache = new Dictionary<string, SourceText>(StringComparer.OrdinalIgnoreCase);

        public ArchiveFileResolver ArchiveFile { get; private set; }

        public SourceFileResolver(IEnumerable<ArchiveFile> files)
        {
            ArchiveFile = new ArchiveFileResolver(files);
            resolvers.Insert(0, ArchiveFile);
        }

        public SourceFileResolver(string logFilePath)
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                var buildSources = Path.ChangeExtension(logFilePath, buildsourceszip);
                if (File.Exists(buildSources))
                {
                    var files = Build.ReadSourceFiles(buildSources);
                    ArchiveFile = new ArchiveFileResolver(files);
                    resolvers.Insert(0, ArchiveFile);
                }
            }
        }

        public bool HasFile(string filePath)
        {
            return GetSourceFileText(filePath) != null;
        }

        public SourceText GetSourceFileText(string filePath)
        {
            if (filePath == null)
            {
                return null;
            }

            lock (fileContentsCache)
            {
                if (fileContentsCache.TryGetValue(filePath, out var result))
                {
                    return result;
                }

                foreach (var resolver in resolvers)
                {
                    var candidate = resolver.GetSourceFileText(filePath);
                    if (candidate != null)
                    {
                        fileContentsCache[filePath] = candidate;
                        return candidate;
                    }
                }

                fileContentsCache[filePath] = null;
                return null;
            }
        }
    }
}
