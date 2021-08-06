using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;

namespace BinlogTool
{
    public class Searcher
    {
        public static void Search(string binlogs, string search)
        {
            if (string.IsNullOrEmpty(binlogs))
            {
                binlogs = "*.binlog";
            }

            binlogs = binlogs.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            string fileName;
            string directory;
            if (binlogs.Contains(Path.DirectorySeparatorChar))
            {
                fileName = Path.GetFileName(binlogs);
                directory = Path.GetDirectoryName(binlogs);
                if (!Path.IsPathRooted(directory))
                {
                    directory = Path.GetFullPath(directory);
                }
            }
            else
            {
                fileName = binlogs;
                directory = Environment.CurrentDirectory;
            }

            var files = Directory.GetFiles(directory, fileName, SearchOption.AllDirectories);
            Search(files, search);
        }

        private static void Search(string[] files, string search)
        {
            foreach (var file in files)
            {
                SearchInFile(file, search);
            }
        }

        private static void SearchInFile(string binlogFilePath, string searchText)
        {
            var build = Serialization.Read(binlogFilePath);
            BuildAnalyzer.AnalyzeBuild(build);
            
            var search = new Search(
                    new[] { build },
                    build.StringTable.Instances,
                    5000,
                    false
                    //, Build.StringTable // disable validation in production
                    );
            var results = search.FindNodes(searchText, CancellationToken.None);
            if (!results.Any())
            {
                return;
            }

            Log.WriteLine(binlogFilePath, ConsoleColor.Cyan);

            var resultTree = ResultTree.BuildResultTree(results);
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