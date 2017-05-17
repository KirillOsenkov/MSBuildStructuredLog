using System.Collections.Generic;
using System.IO;

namespace StructuredLogViewer
{
    public class SourceFileResolver : ISourceFileResolver
    {
        private readonly List<ISourceFileResolver> resolvers = new List<ISourceFileResolver>
        {
            new LocalSourceFileResolver()
        };

        private const string buildsourceszip = ".buildsources.zip";

        public ArchiveFileResolver ArchiveFile { get; private set; }

        public SourceFileResolver(byte[] sourceFilesArchive)
        {
            ArchiveFile = new ArchiveFileResolver(sourceFilesArchive);
            resolvers.Insert(0, ArchiveFile);
        }

        public SourceFileResolver(string logFilePath)
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                var buildSources = Path.ChangeExtension(logFilePath, buildsourceszip);
                if (File.Exists(buildSources))
                {
                    ArchiveFile = new ArchiveFileResolver(buildSources);
                    resolvers.Insert(0, ArchiveFile);
                }
            }
        }

        public string GetSourceFileText(string filePath)
        {
            if (filePath == null)
            {
                return null;
            }

            foreach (var resolver in resolvers)
            {
                var candidate = resolver.GetSourceFileText(filePath);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
