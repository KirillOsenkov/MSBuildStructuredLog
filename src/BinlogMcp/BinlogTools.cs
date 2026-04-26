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

    [McpServerTool(Name = "get_children", ReadOnly = true, Idempotent = true)]
    [Description(@"Returns the immediate children of a node, optionally filtered by kind and/or a name substring, paginated. Each line is: 'kind summary [id]'.

Filtering avoids paging through thousands of children just to find a few interesting ones (e.g. a Project with 10,000 Properties + 200 Targets).

kind: any $-token from the search DSL minus the leading '$' (e.g. ""target"", ""task"", ""property"", ""item"", ""metadata"", ""message"", ""error"", ""warning"", ""csc"", ""rar""). Same matching rules as `search $kind`: '$task' matches all task subtypes; '$csc' matches Task instances named Csc.

nameContains: case-insensitive substring matched against the child's name/text fields (same fields the search tool searches by default).

Returns nothing if the node has no children matching the filter.")]
    public static string GetChildren(
        [Description("Absolute path to a .binlog file")] string path,
        [Description("Node id as returned by search")] string id,
        [Description("Optional node-kind filter, e.g. \"target\", \"task\", \"property\", \"csc\". Mirrors the search DSL's $kind tokens (without the $).")] string kind = null,
        [Description("Optional case-insensitive substring matched against the child's name/text.")] string nameContains = null,
        [Description("Number of leading children (after filtering) to skip (default 0)")] int? skip = null,
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

        // Build a NodeQueryMatcher from the (kind, nameContains) pair so we
        // get exactly the same matching semantics as the search tool.
        StructuredLogViewer.NodeQueryMatcher matcher = null;
        string filterDescription = null;
        kind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim().TrimStart('$');
        nameContains = string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();
        if (kind != null || nameContains != null)
        {
            var queryParts = new List<string>(2);
            if (kind != null)
            {
                queryParts.Add("$" + kind);
            }

            if (nameContains != null)
            {
                // Quote so multi-word substrings are treated as one term.
                queryParts.Add("\"" + nameContains.Replace("\"", "\\\"") + "\"");
            }

            string query = string.Join(" ", queryParts);
            matcher = new StructuredLogViewer.NodeQueryMatcher(query);
            filterDescription = query;
        }

        IEnumerable<BaseNode> source = tree.Children;
        if (matcher != null)
        {
            source = source.Where(c => matcher.IsMatch(c) != null);
        }

        var filtered = source.ToList();
        int total = filtered.Count;

        var sb = new StringBuilder();
        sb.Append("parent: ").AppendLine(FormatNode(node));
        if (filterDescription != null)
        {
            sb.Append("filter: ").AppendLine(filterDescription);
        }

        sb.Append("children: ").Append(Math.Min(take, Math.Max(0, total - offset)))
          .Append(" (skip=").Append(offset)
          .Append(", take=").Append(take)
          .Append(", total=").Append(total);
        if (matcher != null)
        {
            sb.Append(", unfiltered=").Append(tree.Children.Count);
        }

        sb.AppendLine(")");

        int end = Math.Min(total, offset + take);
        for (int i = offset; i < end; i++)
        {
            sb.AppendLine(FormatNode(filtered[i]));
        }

        return sb.ToString();
    });

    [McpServerTool(Name = "get_ancestors", ReadOnly = true, Idempotent = true)]
    [Description("Returns the chain of ancestors of a node from the root down to (but not including) the node itself. Each line is: 'kind summary [id]'. Useful for answering 'where did this happen?'")]
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
        sb.Append("ancestors of ").Append(FormatNode(node)).Append(": ").Append(chain.Count).AppendLine();
        foreach (var ancestor in chain)
        {
            sb.AppendLine(FormatNode(ancestor));
        }

        return sb.ToString();
    });

    public const int DefaultPrintMaxNodes = 500;
    public const int MaxAllowedPrintNodes = 10000;

    [McpServerTool(Name = "print_subtree", ReadOnly = true, Idempotent = true)]
    [Description(@"Renders a node and its descendants as indented text, viewer-style. Each line is: 'kind summary [id]'.

When maxNodes is hit, the trailing hint suggests two ways to continue:
  - drill in: get_children on the truncation point (deeper)
  - continue level: get_children on its parent with skip=N (more siblings at the same level)
When maxDepth is hit, an inline '... N more' marker shows the suppressed children with a get_children hint.")]
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

        void Write(BaseNode n, int depth, BaseNode parent, int indexInParent)
        {
            if (truncated)
            {
                return;
            }

            if (rendered >= nodeBudget)
            {
                truncated = true;
                string nId = NodeId.Get(n) ?? "?";
                var hintBuilder = new StringBuilder();
                hintBuilder.Append("truncated at maxNodes=").Append(nodeBudget).AppendLine(".");
                hintBuilder.Append("  to drill in:        get_children(path, \"").Append(nId).AppendLine("\")");
                if (parent != null && indexInParent >= 0)
                {
                    string parentId = NodeId.Get(parent);
                    if (parentId != null)
                    {
                        hintBuilder.Append("  to continue level:  get_children(path, \"")
                            .Append(parentId).Append("\", skip=").Append(indexInParent).AppendLine(")");
                    }
                }

                truncationHint = hintBuilder.ToString().TrimEnd();
                return;
            }

            rendered++;
            sb.Append(' ', depth * 2);
            sb.AppendLine(FormatNode(n));

            if (depth >= depthLimit)
            {
                if (n is TreeNode { HasChildren: true } tn && !truncated)
                {
                    string nodeIdText = NodeId.Get(n) ?? "?";
                    sb.Append(' ', (depth + 1) * 2);
                    sb.Append("... ").Append(tn.Children.Count).Append(" more (depth limit; call get_children(path, \"")
                      .Append(nodeIdText).AppendLine("\") to drill in)");
                }

                return;
            }

            if (n is TreeNode { HasChildren: true } tree)
            {
                for (int i = 0; i < tree.Children.Count; i++)
                {
                    Write(tree.Children[i], depth + 1, n, i);
                    if (truncated)
                    {
                        return;
                    }
                }
            }
        }

        Write(node, 0, parent: null, indexInParent: -1);

        if (truncationHint != null)
        {
            sb.AppendLine(truncationHint);
        }

        return sb.ToString();
    });

    private static string Summarize(BaseNode node)
    {
        string text = node.GetFullText() ?? node.Title ?? node.ToString() ?? string.Empty;
        return TextUtilities.ShortenValue(text, "...", maxChars: 300);
    }

    /// <summary>
    /// The canonical one-line representation of a real node:
    /// <c>"Kind Summary [id]"</c>. Use this everywhere a node row is
    /// printed (search results, get_children, get_ancestors,
    /// print_subtree, search_properties_and_items) so the format is
    /// uniform and the trailing <c>[id]</c> is always round-trippable.
    /// <para>
    /// <see cref="Property"/> and <see cref="Metadata"/> nodes drop the
    /// kind prefix and use <c>Name=Value [id]</c> instead, since their
    /// kind is implied by context and the value is the interesting part.
    /// </para>
    /// </summary>
    private static string FormatNode(BaseNode node)
    {
        string id = NodeId.Get(node) ?? "?";
        if (node is Property or Metadata)
        {
            var nv = (NameValueNode)node;
            string value = TextUtilities.ShortenValue(nv.Value ?? string.Empty, "...", maxChars: 300);
            return $"{nv.Name}={value} [{id}]";
        }

        string kind = node.TypeName ?? node.GetType().Name;
        string summary = Summarize(node);
        // Some nodes (Import, NoImport) include their kind in GetFullText
        // already (e.g. "Import Foo.targets at (1;1)"). Don't double it up.
        if (summary.StartsWith(kind + " ", StringComparison.Ordinal))
        {
            return $"{summary} [{id}]";
        }

        return $"{kind} {summary} [{id}]";
    }

    private static string DescribeNode(BaseNode node)
    {
        var sb = new StringBuilder();
        // Unlike FormatNode, do not truncate: get_node is the one tool the
        // caller uses precisely to see the full untruncated text of a node.
        string kind = node.TypeName ?? node.GetType().Name;
        string fullText = node.GetFullText() ?? node.Title ?? node.ToString() ?? string.Empty;
        string id = NodeId.Get(node) ?? "?";
        if (fullText.StartsWith(kind + " ", StringComparison.Ordinal))
        {
            sb.Append(fullText).Append(" [").Append(id).Append(']').AppendLine();
        }
        else
        {
            sb.Append(kind).Append(' ').Append(fullText).Append(" [").Append(id).Append(']').AppendLine();
        }

        if (node is TimedNode timed)
        {
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
            sb.Append("parent: ").AppendLine(FormatNode(parent));
        }

        if (node is TreeNode tree)
        {
            sb.Append("childCount: ").AppendLine(tree.Children.Count.ToString());
        }

        return sb.ToString();
    }
}
