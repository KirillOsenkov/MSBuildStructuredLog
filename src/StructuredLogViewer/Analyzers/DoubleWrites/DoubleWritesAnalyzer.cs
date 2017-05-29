using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class DoubleWritesAnalyzer
    {
        private BuildAnalyzer buildAnalyzer;
        private readonly Dictionary<string, HashSet<string>> fileCopySourcesForDestination = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public DoubleWritesAnalyzer(BuildAnalyzer buildAnalyzer)
        {
            this.buildAnalyzer = buildAnalyzer;
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

        public void AnalyzeDoubleWrites(Build build)
        {
            foreach (var bucket in fileCopySourcesForDestination)
            {
                if (IsDoubleWrite(bucket))
                {
                    var doubleWrites = build.GetOrCreateNodeWithName<Folder>("DoubleWrites");
                    var item = new Item { Text = bucket.Key };
                    doubleWrites.AddChild(item);
                    foreach (var source in bucket.Value)
                    {
                        item.AddChild(new Item { Text = source });
                    }
                }
            }
        }

        private void ProcessCopy(string source, string destination)
        {
            HashSet<string> bucket = null;
            if (!fileCopySourcesForDestination.TryGetValue(destination, out bucket))
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
