using System.Collections.Generic;

namespace StructuredLogViewer
{
    public class SourceFileResolver : ISourceFileResolver
    {
        private readonly IEnumerable<ISourceFileResolver> resolvers = new[]
        {
            new LocalSourceFileResolver()
        };

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
