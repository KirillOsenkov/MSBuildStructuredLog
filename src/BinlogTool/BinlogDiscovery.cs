using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BinlogTool
{
    /// <summary>
    /// Discovers binlog files based on paths, patterns, and recursion options.
    /// </summary>
    public static class BinlogDiscovery
    {
        /// <summary>
        /// Discovers binlog files from the given inputs.
        /// </summary>
        /// <param name="binlogPaths">Comma-separated list of paths, patterns, or null for auto-discovery</param>
        /// <param name="recurse">Whether to search subdirectories</param>
        /// <returns>List of discovered binlog file paths</returns>
        public static List<string> DiscoverBinlogs(string binlogPaths, bool recurse)
        {
            var result = new List<string>();

            // If no paths specified, auto-discover
            if (string.IsNullOrWhiteSpace(binlogPaths))
            {
                return DiscoverInDirectory(Environment.CurrentDirectory, recurse);
            }

            // Parse CSV (handle quoted paths with spaces)
            var paths = ParseCsvPaths(binlogPaths);

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    // Direct file path
                    result.Add(Path.GetFullPath(path));
                }
                else if (Directory.Exists(path))
                {
                    // Directory - find all binlogs
                    result.AddRange(DiscoverInDirectory(path, recurse));
                }
                else
                {
                    // Pattern or wildcard
                    var discoveredFiles = FindBinlogsByPattern(path, recurse);
                    result.AddRange(discoveredFiles);
                }
            }

            return result.Distinct().ToList();
        }

        private static List<string> ParseCsvPaths(string csvPaths)
        {
            var result = new List<string>();
            var current = "";
            bool inQuotes = false;

            for (int i = 0; i < csvPaths.Length; i++)
            {
                char c = csvPaths[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        result.Add(current.Trim());
                    }
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                result.Add(current.Trim());
            }

            return result;
        }

        private static List<string> DiscoverInDirectory(string directory, bool recurse)
        {
            try
            {
                var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                return Directory.EnumerateFiles(directory, "*.binlog", searchOption)
                    .Select(Path.GetFullPath)
                    .ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        private static List<string> FindBinlogsByPattern(string pattern, bool recurse)
        {
            try
            {
                pattern = pattern.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                string fileName;
                string directory;

                if (pattern.Contains(Path.DirectorySeparatorChar))
                {
                    fileName = Path.GetFileName(pattern);
                    directory = Path.GetDirectoryName(pattern);
                    if (!Path.IsPathRooted(directory))
                    {
                        directory = Path.GetFullPath(directory);
                    }
                }
                else
                {
                    fileName = pattern;
                    directory = Environment.CurrentDirectory;
                }

                var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                return Directory.EnumerateFiles(directory, fileName, searchOption)
                    .Select(Path.GetFullPath)
                    .ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }
    }
}
