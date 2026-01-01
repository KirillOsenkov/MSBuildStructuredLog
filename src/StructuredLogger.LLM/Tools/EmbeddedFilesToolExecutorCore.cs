using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Core implementation of embedded files tool execution logic.
    /// </summary>
    internal class EmbeddedFilesToolExecutorCore
    {
        private readonly Build build;
        private const int MaxOutputTokensPerTool = 3000;

        public EmbeddedFilesToolExecutorCore(Build build)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
        }

        private string TruncateIfNeeded(string result)
        {
            const int maxChars = MaxOutputTokensPerTool * 4;
            if (result.Length > maxChars)
            {
                return result.Substring(0, maxChars) + "\n\n[Output truncated due to length. Use more specific filters or patterns.]";
            }
            return result;
        }

        public string ListEmbeddedFiles(string? pathPattern = null, int maxResults = 100)
        {
            var sourceFiles = build.SourceFiles;
            
            if (sourceFiles == null || sourceFiles.Count == 0)
            {
                return "No embedded files found in this binlog. The binlog may not have been created with /bl:embed or may not include source files.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== Embedded Files ===");

            Regex? regex = null;
            if (!string.IsNullOrWhiteSpace(pathPattern))
            {
                try
                {
                    regex = new Regex(pathPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
                catch (ArgumentException ex)
                {
                    return $"Error: Invalid regex pattern '{pathPattern}': {ex.Message}";
                }
            }

            var matchingFiles = sourceFiles.AsEnumerable();
            
            if (regex != null)
            {
                matchingFiles = matchingFiles.Where(f => regex.IsMatch(f.FullPath));
            }

            var filesList = matchingFiles.ToList();
            
            if (filesList.Count == 0)
            {
                if (regex != null)
                {
                    return $"No embedded files found matching pattern '{pathPattern}'. Total embedded files: {sourceFiles.Count}";
                }
                return "No embedded files found.";
            }

            sb.AppendLine($"Total files: {sourceFiles.Count}");
            if (regex != null)
            {
                sb.AppendLine($"Matching pattern '{pathPattern}': {filesList.Count}");
            }
            sb.AppendLine();

            // Group files by extension for better readability
            var groupedByExtension = filesList
                .GroupBy(f => System.IO.Path.GetExtension(f.FullPath).ToLowerInvariant())
                .OrderByDescending(g => g.Count());

            foreach (var group in groupedByExtension)
            {
                string extension = string.IsNullOrEmpty(group.Key) ? "(no extension)" : group.Key;
                sb.AppendLine($"[{extension}] ({group.Count()} files):");
                
                int filesShown = 0;
                foreach (var file in group.OrderBy(f => f.FullPath))
                {
                    if (filesShown >= maxResults) break;
                    int lineCount = file.Text.Split('\n').Length;
                    sb.AppendLine($"  - {file.FullPath} ({lineCount} lines)");
                    filesShown++;
                }
                
                if (group.Count() > filesShown)
                {
                    sb.AppendLine($"  ... and {group.Count() - filesShown} more {extension} files");
                }
                sb.AppendLine();
            }

            return TruncateIfNeeded(sb.ToString());
        }

        public string SearchEmbeddedFiles(string searchPattern, string? filePathPattern = null, int maxMatches = 20)
        {
            if (string.IsNullOrWhiteSpace(searchPattern))
            {
                return "Error: Search pattern cannot be empty.";
            }

            var sourceFiles = build.SourceFiles;
            
            if (sourceFiles == null || sourceFiles.Count == 0)
            {
                return "No embedded files found in this binlog.";
            }

            Regex searchRegex;
            try
            {
                searchRegex = new Regex(searchPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (ArgumentException ex)
            {
                return $"Error: Invalid search pattern '{searchPattern}': {ex.Message}";
            }

            Regex? filePathRegex = null;
            if (!string.IsNullOrWhiteSpace(filePathPattern))
            {
                try
                {
                    filePathRegex = new Regex(filePathPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
                catch (ArgumentException ex)
                {
                    return $"Error: Invalid file path pattern '{filePathPattern}': {ex.Message}";
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== Search Results for '{searchPattern}' ===");
            if (filePathRegex != null)
            {
                sb.AppendLine($"File filter: '{filePathPattern}'");
            }
            sb.AppendLine();

            var results = new List<(string filePath, int lineNumber, string line, string context)>();
            int filesSearched = 0;

            foreach (var file in sourceFiles)
            {
                if (filePathRegex != null && !filePathRegex.IsMatch(file.FullPath))
                {
                    continue;
                }

                filesSearched++;
                var lines = file.Text.Split('\n');
                
                for (int i = 0; i < lines.Length && results.Count < maxMatches; i++)
                {
                    var line = lines[i];
                    if (searchRegex.IsMatch(line))
                    {
                        // Get context (1 line before and after)
                        var contextLines = new List<string>();
                        if (i > 0)
                        {
                            contextLines.Add($"  {i}: {lines[i - 1].TrimEnd()}");
                        }
                        contextLines.Add($"> {i + 1}: {line.TrimEnd()}"); // Current line (1-based)
                        if (i < lines.Length - 1)
                        {
                            contextLines.Add($"  {i + 2}: {lines[i + 1].TrimEnd()}");
                        }

                        results.Add((file.FullPath, i + 1, line.TrimEnd(), string.Join("\n", contextLines)));
                    }
                }

                if (results.Count >= maxMatches)
                {
                    break;
                }
            }

            sb.AppendLine($"Files searched: {filesSearched}");
            sb.AppendLine($"Matches found: {results.Count}");
            sb.AppendLine();

            if (results.Count == 0)
            {
                return sb.ToString() + $"No matches found for pattern '{searchPattern}'.";
            }

            foreach (var result in results)
            {
                sb.AppendLine($"File: {result.filePath}");
                sb.AppendLine($"Line {result.lineNumber}:");
                sb.AppendLine(result.context);
                sb.AppendLine();
            }

            if (results.Count >= maxMatches)
            {
                sb.AppendLine($"(Results limited to {maxMatches} matches. Use maxMatches parameter to see more.)");
            }

            return TruncateIfNeeded(sb.ToString());
        }

        public string ReadEmbeddedFileLines(string filePath, int startLine = 1, int endLine = -1, int maxLines = 100)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "Error: File path cannot be empty.";
            }

            var sourceFiles = build.SourceFiles;
            
            if (sourceFiles == null || sourceFiles.Count == 0)
            {
                return "No embedded files found in this binlog.";
            }

            // Find the file (case-insensitive)
            var file = sourceFiles.FirstOrDefault(f => 
                f.FullPath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            if (file == null)
            {
                // Try partial match
                var partialMatches = sourceFiles
                    .Where(f => f.FullPath.IndexOf(filePath, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(5)
                    .ToList();

                if (partialMatches.Any())
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"File '{filePath}' not found. Did you mean one of these?");
                    foreach (var match in partialMatches)
                    {
                        sb.AppendLine($"  - {match.FullPath}");
                    }
                    return sb.ToString();
                }

                return $"File '{filePath}' not found in embedded files. Use ListEmbeddedFiles to see available files.";
            }

            var lines = file.Text.Split('\n');
            int totalLines = lines.Length;

            if (startLine < 1)
            {
                startLine = 1;
            }

            if (endLine == -1 || endLine > totalLines)
            {
                endLine = totalLines;
            }

            if (startLine > totalLines)
            {
                return $"Error: Start line {startLine} is beyond file length ({totalLines} lines).";
            }

            if (startLine > endLine)
            {
                return $"Error: Start line ({startLine}) cannot be greater than end line ({endLine}).";
            }

            // Apply maxLines limit
            int requestedLines = endLine - startLine + 1;
            if (requestedLines > maxLines)
            {
                endLine = startLine + maxLines - 1;
            }

            var result = new StringBuilder();
            result.AppendLine($"=== {file.FullPath} ===");
            result.AppendLine($"Lines {startLine}-{endLine} of {totalLines}");
            result.AppendLine();

            for (int i = startLine - 1; i < endLine && i < totalLines; i++)
            {
                result.AppendLine($"{i + 1,6}: {lines[i].TrimEnd()}");
            }

            if (requestedLines > maxLines)
            {
                result.AppendLine();
                result.AppendLine($"(Output limited to {maxLines} lines. Requested {requestedLines} lines.)");
            }

            return TruncateIfNeeded(result.ToString());
        }
    }
}
