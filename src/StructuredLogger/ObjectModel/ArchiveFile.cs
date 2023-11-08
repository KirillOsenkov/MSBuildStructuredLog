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
            => From(entry, adjustPath: true);

        public static ArchiveFile From(ZipArchiveEntry entry, bool adjustPath)
        {
            var filePath = adjustPath ? CalculateArchivePath(entry.FullName) : entry.FullName;
            var text = GetText(entry);
            var file = new ArchiveFile(filePath, text);
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

        public static string CalculateArchivePath(string filePath)
        {
            string archivePath = filePath;

            if (archivePath.Length > 1 && archivePath[1] == '\\' && archivePath[0] != '\\' && archivePath[0] != '/')
            {
                archivePath = archivePath[0] + ":" + archivePath.Substring(1);
            }

            archivePath = TextUtilities.NormalizeFilePath(archivePath);

            return archivePath;
        }
    }
}
