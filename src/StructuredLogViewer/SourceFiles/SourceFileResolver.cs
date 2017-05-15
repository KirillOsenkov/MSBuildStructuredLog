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

        public SourceFileResolver(string logFilePath)
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                var buildSources = Path.ChangeExtension(logFilePath, buildsourceszip);
                if (File.Exists(buildSources))
                {
                    resolvers.Insert(0, new ArchiveFileResolver(buildSources));
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
