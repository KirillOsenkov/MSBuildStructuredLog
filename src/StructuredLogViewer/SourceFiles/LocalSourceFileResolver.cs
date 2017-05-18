using System.IO;

namespace StructuredLogViewer
{
    public class LocalSourceFileResolver : ISourceFileResolver
    {
        public SourceText GetSourceFileText(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    return new SourceText(File.ReadAllText(filePath));
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
