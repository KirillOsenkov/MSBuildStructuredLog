using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class DoubleWritesAnalyzer
    {
        private readonly Dictionary<string, HashSet<string>> fileCopySourcesForDestination = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public static IEnumerable<KeyValuePair<string, HashSet<string>>> GetDoubleWrites(Build build)
        {
            var analyzer = new DoubleWritesAnalyzer();
            build.VisitAllChildren<Task>(task => analyzer.AnalyzeTask(task));
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

        public void AnalyzeTask(Task task)
        {
            if (task is CopyTask copyTask)
            {
                AnalyzeCopyTask(copyTask);
            }
            else if (task is CscTask cscTask && cscTask.CompilationWrites.HasValue)
            {
                AnalyzeCompilationWrites(cscTask.CompilationWrites.Value);
            }
            else if (task is VbcTask vbcTask && vbcTask.CompilationWrites.HasValue)
            {
                AnalyzeCompilationWrites(vbcTask.CompilationWrites.Value);
            }
            else if (task is FscTask fscTask && fscTask.CompilationWrites.HasValue)
            {
                AnalyzeCompilationWrites(fscTask.CompilationWrites.Value);
            }
        }

        private void AnalyzeCopyTask(CopyTask copyTask)
        {
            foreach (var copyOperation in copyTask.FileCopyOperations)
            {
                if (copyOperation.Copied)
                {
                    ProcessCopy(copyOperation.Source, copyOperation.Destination);
                }
            }
        }

        private void AnalyzeCompilationWrites(CompilationWrites writes)
        {
            var source = writes.AssemblyOrRefAssembly;
            process(writes.Assembly);
            process(writes.RefAssembly);
            process(writes.Pdb);
            process(writes.XmlDocumentation);
            process(writes.SourceLink);

            void process(string destination)
            {
                if (!string.IsNullOrEmpty(destination))
                {
                    ProcessCopy(source, destination);
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
                .Select(f => GetFullPath(f))
                .Distinct()
                .Count() == 1)
            {
                return false;
            }

            return true;
        }

        private static string GetFullPath(string filePath)
        {
            try
            {
                filePath = new FileInfo(filePath).FullName;
            }
            // https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/679
            catch
            {
            }

            return filePath;
        }
    }
}
