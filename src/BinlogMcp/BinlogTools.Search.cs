using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
    [Description(@"Searches a binlog using the MSBuild Structured Log Viewer query syntax and returns matching nodes as an indented tree, mirroring what the viewer shows in its Search panel. The binlog is loaded implicitly if not already cached.

Output shape:
  N result(s) (skip=A, take=B, matched=C)
  <project / target / task / ... lines, indented with 2 spaces per level>

Real (addressable) nodes have their id appended in square brackets, e.g.
  Project StructuredLogger.csproj net10.0 → Build [123]
    Target CoreCompile [124]
      Task Csc [125]
Use ids with get_node, get_children, get_ancestors, print_subtree.

Synthetic nodes (notes, totals, NuGet/$copy result groupings, etc.) print verbatim with no id.

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
        sb.Append(total).Append(total == 1 ? " result" : " results");
        sb.Append(" (skip=").Append(offset)
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

        // ResultTree groups results under their nearest Project/Target/Task,
        // mirroring the viewer's Search panel. addDuration=false suppresses
        // its own "N results" Note since we already emit a header line.
        var tree = ResultTree.BuildResultTree(page, addDuration: false);
        foreach (var child in tree.Children)
        {
            AppendNode(sb, child, depth: 0);
        }

        return sb.ToString();
    });

    private const int IndentSpaces = 2;

    /// <summary>
    /// Pretty-prints a search result subtree. <see cref="ProxyNode"/> wrappers
    /// (created by <see cref="ResultTree.BuildResultTree"/>) are unwrapped so
    /// real nodes get their <c>[id]</c> appended; everything else prints
    /// verbatim.
    /// </summary>
    private static void AppendNode(StringBuilder sb, BaseNode node, int depth)
    {
        for (int i = 0; i < depth * IndentSpaces; i++)
        {
            sb.Append(' ');
        }

        if (node is ProxyNode proxy)
        {
            if (proxy.Original is NameValueNode nv)
            {
                string name = TextUtilities.ShortenValue(nv.Name ?? string.Empty, "...", maxChars: 300);
                string value = TextUtilities.ShortenValue(nv.Value ?? string.Empty, "...", maxChars: 300);
                sb.Append(name).Append('=').Append(value);
                string id = NodeId.Get(nv);
                if (id != null)
                {
                    sb.Append(" [").Append(id).Append(']');
                }

                sb.AppendLine();
            }
            else
            {
                string text = (proxy.Text ?? string.Empty).TrimEnd();
                sb.Append(text);
                if (proxy.Original is { } original)
                {
                    string id = NodeId.Get(original);
                    if (id != null)
                    {
                        sb.Append(" [").Append(id).Append(']');
                    }
                }

                sb.AppendLine();
            }
        }
        else
        {
            string text = (node.GetFullText() ?? node.Title ?? string.Empty).TrimEnd();
            sb.AppendLine(text);
        }

        if (node is TreeNode treeNode && treeNode.HasChildren)
        {
            foreach (var child in treeNode.Children)
            {
                AppendNode(sb, child, depth + 1);
            }
        }
    }

    [McpServerTool(Name = "search_properties_and_items", ReadOnly = true, Idempotent = true)]
    [Description(@"Searches the Properties, Items, property assignments, and property reassignments folders scoped to a single Project or ProjectEvaluation node. Mirrors the viewer's ""Properties and Items"" tab.

Use this to answer focused questions like ""what's the value of OutputPath in this project?"" or ""where does TargetFramework get set during this evaluation?"" without scanning the entire build.

Accepts the same query DSL as the search tool (call get_search_syntax_help). The context is the node id of a Project or ProjectEvaluation (find one with `search $project Foo` or `search $projectevaluation Foo`).

Each result line is: 'kind summary [id]'")]
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
        sb.Append("context: ").AppendLine(FormatNode(contextNode));
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

        var tree = ResultTree.BuildResultTree(page, addDuration: false);
        foreach (var child in tree.Children)
        {
            AppendNode(sb, child, depth: 0);
        }

        return sb.ToString();
    });

    [McpServerTool(Name = "get_search_syntax_help", ReadOnly = true, Idempotent = true)]
    [Description("Returns the full reference for the MSBuild Structured Log Viewer search query syntax used by the search tool. Call this once at the start of a session to learn what the search tool can do.")]
    public static string GetSearchSyntaxHelp() => SearchSyntaxHelpText;

    private const string ResourceName = "SearchSyntax.md";

    private static string searchSyntaxHelpText;
    public static string SearchSyntaxHelpText
    {
        get
        {
            if (searchSyntaxHelpText != null)
            {
                return searchSyntaxHelpText;
            }

            var assembly = typeof(BinlogTools).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{ResourceName}' not found in {assembly.FullName}.");
            using var reader = new StreamReader(stream);
            searchSyntaxHelpText = reader.ReadToEnd();
            return searchSyntaxHelpText;
        }
    }
}
