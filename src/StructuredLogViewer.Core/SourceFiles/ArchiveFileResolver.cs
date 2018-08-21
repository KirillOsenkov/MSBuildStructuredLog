﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace StructuredLogViewer.Core.SourceFiles
{
    public class ArchiveFileResolver : ISourceFileResolver
    {
        private readonly Dictionary<string, SourceText> fileContents = new Dictionary<string, SourceText>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, SourceText> Files => fileContents;

        public ArchiveFileResolver(string zipFullPath)
        {
            using (var stream = new FileStream(zipFullPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                ExtractFilesFromStream(stream);
            }
        }

        public ArchiveFileResolver(byte[] sourceFilesArchive)
        {
            using (var stream = new MemoryStream(sourceFilesArchive))
            {
                ExtractFilesFromStream(stream);
            }
        }

        private void ExtractFilesFromStream(Stream stream)
        {
            try
            {
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
            catch
            {
                // The archive is likely incomplete (corrupt) because the build crashed.
                // Tolerate this situation.
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

        public SourceText GetSourceFileText(string filePath)
        {
            filePath = CalculateArchivePath(filePath);
            fileContents.TryGetValue(filePath, out var result);
            return result;
        }

        private void AddFile(string fullName, string text)
        {
            fileContents[fullName] = new SourceText(text);
        }
    }
}
