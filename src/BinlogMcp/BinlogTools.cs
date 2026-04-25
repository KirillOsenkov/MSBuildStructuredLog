using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using StructuredLogViewer;

namespace BinlogMcp;

[McpServerToolType]
public static partial class BinlogTools
{
    private static readonly BinlogCache Cache = new();

    public const int DefaultMaxResults = 200;
    public const int MaxAllowedResults = 5000;

    // The MCP SDK only surfaces the original exception message to the
    // client when the thrown exception derives from McpException;
    // anything else is replaced with a generic "An error occurred
    // invoking '<tool>'." Wrap every tool body so LLMs see actionable
    // diagnostics ("file not found", "id out of range", etc.).
    private static T Run<T>(Func<T> body)
    {
        try
        {
            return body();
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException(ex.Message, ex);
        }
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

    [McpServerTool(Name = "get_node", ReadOnly = true, Idempotent = true)]
    [Description("Returns metadata for a single node: kind, summary, parent id, child count, source location, and (for TimedNode) start/end/duration. Does not return children — use get_children.")]
    public static string GetNode(
        [Description("Absolute path to a .binlog file")] string path,
        [Description("Node id as returned by search")] string id) => Run(() =>
    {
        var entry = Cache.Load(path);
        var node = NodeId.Resolve(entry, id);
        return DescribeNode(node);
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

    [McpServerTool(Name = "get_children", ReadOnly = true, Idempotent = true)]
    [Description("Returns the immediate children of a node, paginated. Each line is: [id]<TAB>kind<TAB>summary. Returns nothing if the node has no children.")]
    public static string GetChildren(
        [Description("Absolute path to a .binlog file")] string path,
        [Description("Node id as returned by search")] string id,
        [Description("Number of leading children to skip (default 0)")] int? skip = null,
        [Description("Maximum number of children to return (default 200, max 5000)")] int? maxResults = null) => Run(() =>
    {
        int offset = Math.Max(skip ?? 0, 0);
        int take = Math.Clamp(maxResults ?? DefaultMaxResults, 1, MaxAllowedResults);

        var entry = Cache.Load(path);
        var node = NodeId.Resolve(entry, id);

        if (node is not TreeNode tree || !tree.HasChildren)
        {
            return $"node [{id}] has no children";
        }

        var children = tree.Children;
        int total = children.Count;

        var sb = new StringBuilder();
        sb.Append("parent: [").Append(id).Append("] ").AppendLine(node.TypeName ?? node.GetType().Name);
        sb.Append("children: ").Append(Math.Min(take, Math.Max(0, total - offset)))
          .Append(" (skip=").Append(offset)
          .Append(", take=").Append(take)
          .Append(", total=").Append(total)
          .AppendLine(")");

        int end = Math.Min(total, offset + take);
        for (int i = offset; i < end; i++)
        {
            var child = children[i];
            string childId = NodeId.Get(child) ?? "?";
            string kind = child.TypeName ?? child.GetType().Name;
            sb.Append('[').Append(childId).Append("]\t")
              .Append(kind).Append('\t')
              .AppendLine(Summarize(child));
        }

        return sb.ToString();
    });

    [McpServerTool(Name = "get_ancestors", ReadOnly = true, Idempotent = true)]
    [Description("Returns the chain of ancestors of a node from the root down to (but not including) the node itself. Each line is: [id]<TAB>kind<TAB>summary. Useful for answering 'where did this happen?'")]
    public static string GetAncestors(
        [Description("Absolute path to a .binlog file")] string path,
        [Description("Node id as returned by search")] string id) => Run(() =>
    {
        var entry = Cache.Load(path);
        var node = NodeId.Resolve(entry, id);

        var chain = new List<BaseNode>();
        var current = node.Parent;
        while (current != null)
        {
            chain.Add(current);
            current = current.Parent;
        }

        chain.Reverse();

        if (chain.Count == 0)
        {
            return $"node [{id}] has no ancestors (it is the root)";
        }

        var sb = new StringBuilder();
        sb.Append("ancestors of [").Append(id).Append("]: ").Append(chain.Count).AppendLine();
        foreach (var ancestor in chain)
        {
            string ancestorId = NodeId.Get(ancestor) ?? "?";
            string kind = ancestor.TypeName ?? ancestor.GetType().Name;
            sb.Append('[').Append(ancestorId).Append("]\t")
              .Append(kind).Append('\t')
              .AppendLine(Summarize(ancestor));
        }

        return sb.ToString();
    });

    public const int DefaultPrintMaxNodes = 500;
    public const int MaxAllowedPrintNodes = 10000;

    [McpServerTool(Name = "print_subtree", ReadOnly = true, Idempotent = true)]
    [Description("Renders a node and its descendants as indented text, viewer-style. Each printed node is prefixed with its [id]. Truncated when either maxDepth or maxNodes is hit; the trailing line says how to continue (e.g. call get_children on a deeper node).")]
    public static string PrintSubtree(
        [Description("Absolute path to a .binlog file")] string path,
        [Description("Node id as returned by search")] string id,
        [Description("Maximum tree depth to render relative to the root node (default unlimited)")] int? maxDepth = null,
        [Description("Maximum number of nodes to render (default 500, max 10000)")] int? maxNodes = null) => Run(() =>
    {
        int nodeBudget = Math.Clamp(maxNodes ?? DefaultPrintMaxNodes, 1, MaxAllowedPrintNodes);
        int depthLimit = maxDepth ?? int.MaxValue;

        var entry = Cache.Load(path);
        var node = NodeId.Resolve(entry, id);

        var sb = new StringBuilder();
        int rendered = 0;
        bool truncated = false;
        string truncationHint = null;

        void Write(BaseNode n, int depth)
        {
            if (truncated)
            {
                return;
            }

            if (rendered >= nodeBudget)
            {
                truncated = true;
                string nId = NodeId.Get(n) ?? "?";
                truncationHint = $"truncated at maxNodes={nodeBudget}; resume with get_children(path, \"{nId}\") or print_subtree(path, \"{nId}\")";
                return;
            }

            rendered++;
            sb.Append(' ', depth * 2);
            string nodeIdText = NodeId.Get(n) ?? "?";
            sb.Append('[').Append(nodeIdText).Append("] ").AppendLine(Summarize(n));

            if (depth >= depthLimit)
            {
                if (n is TreeNode { HasChildren: true } tn && !truncated)
                {
                    sb.Append(' ', (depth + 1) * 2);
                    sb.Append("... ").Append(tn.Children.Count).Append(" more (depth limit; call get_children(path, \"")
                      .Append(nodeIdText).AppendLine("\") to drill in)");
                }

                return;
            }

            if (n is TreeNode { HasChildren: true } tree)
            {
                foreach (var child in tree.Children)
                {
                    Write(child, depth + 1);
                    if (truncated)
                    {
                        return;
                    }
                }
            }
        }

        Write(node, 0);

        if (truncationHint != null)
        {
            sb.AppendLine(truncationHint);
        }

        return sb.ToString();
    });

    [McpServerTool(Name = "get_search_syntax_help", ReadOnly = true, Idempotent = true)]
    [Description("Returns the full reference for the MSBuild Structured Log Viewer search query syntax used by the search tool. Call this once at the start of a session to learn what the search tool can do.")]
    public static string GetSearchSyntaxHelp() => SearchSyntaxHelp.Text;

    private static string Summarize(BaseNode node)
    {
        string text = node.Title ?? node.ToString() ?? string.Empty;
        return TextUtilities.ShortenValue(text, "...", maxChars: 300);
    }

    private static string DescribeNode(BaseNode node)
    {
        var sb = new StringBuilder();
        string id = NodeId.Get(node) ?? "?";
        sb.Append("id: ").AppendLine(id);
        sb.Append("kind: ").AppendLine(node.TypeName ?? node.GetType().Name);
        sb.Append("summary: ").AppendLine(Summarize(node));

        if (node is NameValueNode nv)
        {
            sb.Append("name: ").AppendLine(nv.Name);
            sb.Append("value: ").AppendLine(TextUtilities.ShortenValue(nv.Value, "...", maxChars: 1000));
        }

        if (node is TimedNode timed)
        {
            sb.Append("index: ").AppendLine(timed.Index.ToString());
            sb.Append("start: ").AppendLine(timed.StartTime.ToString("o"));
            sb.Append("end: ").AppendLine(timed.EndTime.ToString("o"));
            sb.Append("duration: ").AppendLine(timed.Duration.ToString());
        }

        if (node is IHasSourceFile sf && !string.IsNullOrEmpty(sf.SourceFilePath))
        {
            sb.Append("sourceFile: ").AppendLine(sf.SourceFilePath);
        }

        if (node is IHasLineNumber ln && ln.LineNumber is int lineNumber)
        {
            sb.Append("line: ").AppendLine(lineNumber.ToString());
        }

        if (node.Parent is BaseNode parent)
        {
            string parentId = NodeId.Get(parent) ?? "?";
            sb.Append("parent: [").Append(parentId).Append("] ").AppendLine(parent.TypeName ?? parent.GetType().Name);
        }

        if (node is TreeNode tree)
        {
            sb.Append("childCount: ").AppendLine(tree.Children.Count.ToString());
        }

        return sb.ToString();
    }
}
