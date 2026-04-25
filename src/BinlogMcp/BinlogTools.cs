using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using ModelContextProtocol.Server;
using StructuredLogViewer;

namespace BinlogMcp
{
    [McpServerToolType]
    public static class BinlogTools
    {
        private static readonly BinlogCache Cache = new();

        public const int DefaultMaxResults = 200;
        public const int MaxAllowedResults = 5000;

        [McpServerTool(Name = "load_binlog", ReadOnly = true, Idempotent = true)]
        [Description("Loads an MSBuild .binlog file into memory and returns a summary. Optional: any tool that takes a binlog path will load it implicitly if not already cached. Use this to warm the cache or to inspect the build summary up front.")]
        public static string LoadBinlog(
            [Description("Absolute path to a .binlog file")] string path)
        {
            var entry = Cache.Load(path);
            return Describe(entry);
        }

        [McpServerTool(Name = "reload_binlog", ReadOnly = true, Idempotent = true)]
        [Description("Re-reads a binlog from disk, replacing the cached version. Use this after a rebuild has overwritten the binlog file.")]
        public static string ReloadBinlog(
            [Description("Absolute path to a .binlog file")] string path)
        {
            var entry = Cache.Load(path, forceReload: true);
            return Describe(entry);
        }

        [McpServerTool(Name = "unload_binlog", ReadOnly = true, Idempotent = true)]
        [Description("Evicts a single binlog from the cache to free memory.")]
        public static string UnloadBinlog(
            [Description("Absolute path to the .binlog file to evict")] string path)
        {
            return Cache.Unload(path) ? $"unloaded {path}" : $"not loaded: {path}";
        }

        [McpServerTool(Name = "unload_all_binlogs", ReadOnly = true, Idempotent = true)]
        [Description("Evicts all loaded binlogs from the cache to free memory.")]
        public static string UnloadAllBinlogs()
        {
            int count = Cache.UnloadAll();
            return $"unloaded {count} binlog(s)";
        }

        [McpServerTool(Name = "list_loaded_binlogs", ReadOnly = true, Idempotent = true)]
        [Description("Lists all binlogs currently loaded in the cache, with file sizes and estimated memory usage.")]
        public static string ListLoadedBinlogs()
        {
            var entries = Cache.List();
            if (entries.Count == 0)
            {
                return "no binlogs loaded";
            }

            var lines = entries
                .OrderByDescending(e => e.LastAccessedUtc)
                .Select(e => $"{e.Path}\tfileSize={e.FileSize:n0}\testMem={e.EstimatedMemoryBytes:n0}\tlastAccessed={e.LastAccessedUtc:o}");
            return string.Join("\n", lines);
        }

        private static string Describe(LoadedBinlog entry)
        {
            var build = entry.Build;
            return string.Join("\n", new[]
            {
                $"path: {entry.Path}",
                $"fileSize: {entry.FileSize:n0} bytes",
                $"estimatedMemory: {entry.EstimatedMemoryBytes:n0} bytes",
                $"succeeded: {build.Succeeded}",
                $"duration: {build.Duration}",
                $"msbuildVersion: {build.MSBuildVersion}",
            });
        }

        [McpServerTool(Name = "search", ReadOnly = true, Idempotent = true)]
        [Description(@"Searches a binlog using the MSBuild Structured Log Viewer query syntax and returns matching node ids. The binlog is loaded implicitly if not already cached.

Each result line is: [id]<TAB>kind<TAB>summary

Use the returned ids with get_node, get_children, get_ancestors, print_subtree.

Query syntax cheat sheet (call get_search_syntax_help for the full reference):
  $error                       all errors
  $warning                     all warnings
  $task Csc                    all Csc task invocations
  $target Build                all Build targets
  $project MyProj              all projects whose name contains MyProj
  under($project MyProj) CS1234   nodes containing CS1234 under MyProj
  notunder($task Csc) error    errors not under a Csc task
  $time>500ms                  timed nodes longer than 500ms
  ""exact phrase""             literal substring match
  name=Configuration value=Debug   precise field match
  $42                          node with Index 42")]
        public static string Search(
            [Description("Absolute path to a loaded .binlog file")] string path,
            [Description("Search query in MSBuild Structured Log Viewer syntax")] string query,
            [Description("Maximum number of results to return (default 200, max 5000)")] int? maxResults = null,
            [Description("Number of leading results to skip for paging (default 0)")] int? skip = null)
        {
            int take = Math.Clamp(maxResults ?? DefaultMaxResults, 1, MaxAllowedResults);
            int offset = Math.Max(skip ?? 0, 0);

            var entry = Cache.Load(path);

            var index = entry.Build.SearchIndex;
            if (index == null)
            {
                throw new InvalidOperationException(
                    $"Binlog has no SearchIndex: {path}. Try reload_binlog.");
            }

            // SearchIndex.FindNodes is not thread-safe (mutates typeKeyword,
            // bit vector). Serialize calls per build.
            IReadOnlyList<SearchResult> results;
            lock (index)
            {
                index.MaxResults = offset + take;
                results = index.FindNodes(query, CancellationToken.None).ToArray();
            }

            int total = results.Count;
            var page = results.Skip(offset).Take(take).ToArray();

            var sb = new StringBuilder();
            sb.Append("query: ").AppendLine(query);
            sb.Append("returned: ").Append(page.Length)
              .Append(" (skip=").Append(offset)
              .Append(", take=").Append(take)
              .Append(", matched=").Append(total);
            if (total >= offset + take)
            {
                sb.Append("+");
            }

            sb.AppendLine(")");

            if (page.Length == 0)
            {
                sb.AppendLine("(no results)");
                return sb.ToString();
            }

            foreach (var result in page)
            {
                var node = result.Node;
                if (node == null)
                {
                    continue;
                }

                string id = NodeId.Get(node) ?? "?";
                string kind = node.TypeName ?? node.GetType().Name;
                string summary = Summarize(node);
                sb.Append('[').Append(id).Append("]\t")
                  .Append(kind).Append('\t')
                  .AppendLine(summary);
            }

            return sb.ToString();
        }

        private static string Summarize(BaseNode node)
        {
            string text = node.Title ?? node.ToString() ?? string.Empty;
            return TextUtilities.ShortenValue(text, "...", maxChars: 300);
        }
    }
}
