using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;

namespace BinlogTool
{
    public class Searcher : BinlogToolCommandBase
    {
        public static void Search(string binlogs, string search)
            => new Searcher().Search2(binlogs, search);

        public void Search2(string binlogs, string search)
        {
            var files = FindBinlogs(binlogs, recurse: true).ToList();
            Search(files, search);
        }

        public static IEnumerable<string> FindBinlogs(string inputPath, bool recurse)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                inputPath = "*.binlog";
            }

            inputPath = inputPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (File.Exists(inputPath))
            {
                return new[] { inputPath };
            }

            if (Directory.Exists(inputPath))
            {
                inputPath = Path.Combine(inputPath, "*.binlog");
            }

            string fileName;
            string directory;
            if (inputPath.Contains(Path.DirectorySeparatorChar))
            {
                fileName = Path.GetFileName(inputPath);
                directory = Path.GetDirectoryName(inputPath);
                if (!Path.IsPathRooted(directory))
                {
                    directory = Path.GetFullPath(directory);
                }
            }
            else
            {
                fileName = inputPath;
                directory = Environment.CurrentDirectory;
            }

            return Directory.EnumerateFiles(directory, fileName,
                new EnumerationOptions() { IgnoreInaccessible = true, RecurseSubdirectories = recurse, });
        }

        private void Search(IEnumerable<string> files, string search)
        {
            foreach (var file in files)
            {
                SearchInFile(file, search);
            }
        }

        private void SearchInFile(string binlogFilePath, string searchText)
        {
            var build = this.ReadBuild(binlogFilePath);
            BuildAnalyzer.AnalyzeBuild(build);
            
            var search = new Search(
                    new[] { build },
                    build.StringTable.Instances,
                    5000,
                    markResultsInTree: false);
            var results = search.FindNodes(searchText, CancellationToken.None);
            if (!results.Any())
            {
                return;
            }

            Log.WriteLine(binlogFilePath, ConsoleColor.Cyan);

            var resultTree = ResultTree.BuildResultTree(results, addWhenNoResults: () => new Message { Text = "No results found." });
            PrintTree(resultTree);
            Log.WriteLine("====================================", ConsoleColor.Green);
            Log.WriteLine("");
        }

        private const int IndentSize = 2;

        public static void PrintTree(BaseNode node, int indent = 0)
        {
            string indentText = new string(' ', indent * IndentSize);
            Log.WriteLine(indentText + node.ToString());
            if (node is TreeNode treeNode)
            {
                foreach (var child in treeNode.Children)
                {
                    PrintTree(child, indent + IndentSize);
                }
            }
        }
    }
}
