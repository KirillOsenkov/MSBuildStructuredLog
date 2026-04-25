using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using ModelContextProtocol.Server;
using StructuredLogViewer;

namespace BinlogMcp;

public static partial class BinlogTools
{
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
  $task $time                  tasks, with durations, sorted slowest first
  ""exact phrase""             literal substring match
  name=Configuration value=Debug   precise field match
  $42                          node with Index 42")]
    public static string Search(
        [Description("Absolute path to a loaded .binlog file")] string path,
        [Description("Search query in MSBuild Structured Log Viewer syntax")] string query,
        [Description("Maximum number of results to return (default 200, max 5000)")] int? maxResults = null,
        [Description("Number of leading results to skip for paging (default 0)")] int? skip = null) => Run(() =>
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
    });

    [McpServerTool(Name = "search_properties_and_items", ReadOnly = true, Idempotent = true)]
    [Description(@"Searches the Properties, Items, property assignments, and property reassignments folders scoped to a single Project or ProjectEvaluation node. Mirrors the viewer's ""Properties and Items"" tab.

Use this to answer focused questions like ""what's the value of OutputPath in this project?"" or ""where does TargetFramework get set during this evaluation?"" without scanning the entire build.

Accepts the same query DSL as the search tool (call get_search_syntax_help). The context is the node id of a Project or ProjectEvaluation (find one with `search $project Foo` or `search $projectevaluation Foo`).

Each result line is: [id]<TAB>kind<TAB>summary")]
    public static string SearchPropertiesAndItems(
        [Description("Absolute path to a loaded .binlog file")] string path,
        [Description("Node id of a Project or ProjectEvaluation to scope the search to")] string contextId,
        [Description("Search query in MSBuild Structured Log Viewer syntax")] string query,
        [Description("Maximum number of results to return (default 200, max 5000)")] int? maxResults = null,
        [Description("Number of leading results to skip for paging (default 0)")] int? skip = null) => Run(() =>
    {
        int take = Math.Clamp(maxResults ?? DefaultMaxResults, 1, MaxAllowedResults);
        int offset = Math.Max(skip ?? 0, 0);

        var entry = Cache.Load(path);
        var contextNode = NodeId.Resolve(entry, contextId);

        if (contextNode is not TimedNode timedContext || contextNode is not IProjectOrEvaluation)
        {
            throw new InvalidOperationException(
                $"Node [{contextId}] is a {contextNode.GetType().Name}, but search_properties_and_items requires a Project or ProjectEvaluation. Find one with `search $project Foo` or `search $projectevaluation Foo`.");
        }

        // Reuse the per-binlog PropertiesAndItemsSearch so PropertyGraph's
        // AugmentResults subscriber runs (it appends a "Property Graph"
        // folder when the query selects only Property nodes).
        var search = entry.PropertiesAndItemsSearch;
        IReadOnlyList<SearchResult> results;
        lock (entry)
        {
            results = search.Search(
                timedContext,
                query,
                maxResults: offset + take,
                markResultsInTree: false,
                CancellationToken.None).ToArray();
        }

        int total = results.Count;
        var page = results.Skip(offset).Take(take).ToArray();

        var sb = new StringBuilder();
        sb.Append("query: ").AppendLine(query);
        sb.Append("context: [").Append(contextId).Append("] ").AppendLine(contextNode.TypeName ?? contextNode.GetType().Name);
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
    });

    [McpServerTool(Name = "get_search_syntax_help", ReadOnly = true, Idempotent = true)]
    [Description("Returns the full reference for the MSBuild Structured Log Viewer search query syntax used by the search tool. Call this once at the start of a session to learn what the search tool can do.")]
    public static string GetSearchSyntaxHelp() => SearchSyntaxHelp.Text;
}
