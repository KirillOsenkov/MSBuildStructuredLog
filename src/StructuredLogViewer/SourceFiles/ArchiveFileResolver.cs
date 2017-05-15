using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace StructuredLogViewer
{
    public class ArchiveFileResolver : ISourceFileResolver
    {
        private readonly string zipFullPath;
        private readonly Dictionary<string, string> fileContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ArchiveFileResolver(string zipFullPath)
        {
            this.zipFullPath = zipFullPath;

            using (var stream = new FileStream(zipFullPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zipArchive.Entries)
                {
                    using (var contentStream = entry.Open())
                    using (var reader = new StreamReader(contentStream))
                    {
                        var text = reader.ReadToEnd();
                        AddFile(entry.FullName, text);
                    }
                }
            }
        }

        private static string CalculateArchivePath(string filePath)
        {
            string archivePath = filePath;

            archivePath = archivePath.Replace(":", "");
            archivePath = archivePath.Replace("\\\\", "\\");
            archivePath = archivePath.Replace("/", "\\");

            return archivePath;
        }

        public string GetSourceFileText(string filePath)
        {
            filePath = CalculateArchivePath(filePath);
            string result;
            fileContents.TryGetValue(filePath, out result);
            return result;
        }

        private void AddFile(string fullName, string text)
        {
            fileContents[fullName] = text;
        }
    }
}
