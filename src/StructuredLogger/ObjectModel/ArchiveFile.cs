using System.IO;
using System.IO.Compression;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ArchiveFile
    {
        public ArchiveFile(string fullPath, string text)
        {
            FullPath = fullPath;
            Text = text;
        }

        public string FullPath { get; }
        public string Text { get; }

        public static ArchiveFile From(ZipArchiveEntry entry)
        {
            var text = GetText(entry);
            var file = new ArchiveFile(entry.FullName, text);
            return file;
        }

        public static string GetText(ZipArchiveEntry entry)
        {
            using (var contentStream = entry.Open())
            using (var reader = new StreamReader(contentStream))
            {
                var text = reader.ReadToEnd();
                return text;
            }
        }
    }
}