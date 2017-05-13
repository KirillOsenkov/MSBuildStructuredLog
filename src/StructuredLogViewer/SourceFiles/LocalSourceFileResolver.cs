using System.IO;

namespace StructuredLogViewer
{
    public class LocalSourceFileResolver : ISourceFileResolver
    {
        public string GetSourceFileText(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
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
