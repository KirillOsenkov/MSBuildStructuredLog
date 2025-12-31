using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.LLM
{
    /// <summary>
    /// Async wrapper for EmbeddedFilesToolExecutor that ensures all tool methods execute on background threads.
    /// This prevents UI freezing when the LLM calls these tools.
    /// </summary>
    public class AsyncEmbeddedFilesToolExecutor
    {
        private readonly EmbeddedFilesToolExecutor innerExecutor;

        public AsyncEmbeddedFilesToolExecutor(Build build)
        {
            this.innerExecutor = new EmbeddedFilesToolExecutor(build);
        }

        [Description("Lists all embedded files in the binlog with their paths. Optionally filters by regex pattern on file paths.")]
        public async System.Threading.Tasks.Task<string> ListEmbeddedFiles(
            [Description("Optional regex pattern to filter file paths (e.g., '\\.cs$' for C# files, 'MyProject' for files containing MyProject in path)")] 
            string pathPattern = null)
        {
            return await System.Threading.Tasks.Task.Run(() => innerExecutor.ListEmbeddedFiles(pathPattern)).ConfigureAwait(false);
        }

        [Description("Searches for text patterns within embedded files using regex. Returns matching lines with context. Can optionally filter which files to search.")]
        public async System.Threading.Tasks.Task<string> SearchEmbeddedFiles(
            [Description("Regex pattern to search for in file contents (e.g., 'class\\s+\\w+', 'TODO', 'namespace')")] 
            string searchPattern,
            [Description("Optional regex pattern to filter which files to search by path (e.g., '\\.cs$' for C# files)")] 
            string filePathPattern = null,
            [Description("Maximum number of matches to return (default 20)")] 
            int maxMatches = 20)
        {
            return await System.Threading.Tasks.Task.Run(() => innerExecutor.SearchEmbeddedFiles(searchPattern, filePathPattern, maxMatches)).ConfigureAwait(false);
        }

        [Description("Reads a specific range of lines from an embedded file. Use this to view file contents.")]
        public async System.Threading.Tasks.Task<string> ReadEmbeddedFileLines(
            [Description("Full path of the embedded file to read (case-insensitive)")] 
            string filePath,
            [Description("Starting line number (1-based, inclusive). Defaults to 1 (beginning of file).")] 
            int startLine = 1,
            [Description("Ending line number (1-based, inclusive). If not specified or -1, reads to end of file.")] 
            int endLine = -1,
            [Description("Maximum number of lines to return (default 100). Prevents reading too much content at once.")] 
            int maxLines = 100)
        {
            return await System.Threading.Tasks.Task.Run(() => innerExecutor.ReadEmbeddedFileLines(filePath, startLine, endLine, maxLines)).ConfigureAwait(false);
        }
    }
}
