﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Core.Analyzers
{
    public class DoubleWritesAnalyzer
    {
        private readonly Dictionary<string, HashSet<string>> fileCopySourcesForDestination = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public static IEnumerable<KeyValuePair<string, HashSet<string>>> GetDoubleWrites(Build build)
        {
            var analyzer = new DoubleWritesAnalyzer();
            build.VisitAllChildren<CopyTask>(copyTask => analyzer.AnalyzeFileCopies(copyTask));
            return analyzer.GetDoubleWrites();
        }

        public IEnumerable<KeyValuePair<string, HashSet<string>>> GetDoubleWrites()
        {
            return fileCopySourcesForDestination.Where(IsDoubleWrite);
        }

        public void AppendDoubleWritesFolder(Build build)
        {
            Folder doubleWrites = null;
            foreach (var bucket in GetDoubleWrites())
            {
                doubleWrites = doubleWrites ?? build.GetOrCreateNodeWithName<Folder>("DoubleWrites");
                var item = new Item { Text = bucket.Key };
                doubleWrites.AddChild(item);
                foreach (var source in bucket.Value)
                {
                    item.AddChild(new Item { Text = source });
                }
            }
        }

        public void AnalyzeFileCopies(CopyTask copyTask)
        {
            foreach (var copyOperation in copyTask.FileCopyOperations)
            {
                if (copyOperation.Copied)
                {
                    ProcessCopy(copyOperation.Source, copyOperation.Destination);
                }
            }
        }

        private void ProcessCopy(string source, string destination)
        {
            if (!fileCopySourcesForDestination.TryGetValue(destination, out var bucket))
            {
                bucket = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                fileCopySourcesForDestination.Add(destination, bucket);
            }

            bucket.Add(source);
        }

        private static bool IsDoubleWrite(KeyValuePair<string, HashSet<string>> bucket)
        {
            if (bucket.Value.Count < 2)
            {
                return false;
            }

            if (bucket.Value
                .Select(f => new FileInfo(f))
                .Select(f => f.FullName)
                .Distinct()
                .Count() == 1)
            {
                return false;
            }

            return true;
        }
    }
}
