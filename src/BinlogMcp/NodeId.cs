using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogMcp;

/// <summary>
/// Round-trippable identifier for any <see cref="BaseNode"/> in a loaded
/// build.
/// <para>
/// For a <see cref="TimedNode"/> the id is its <see cref="TimedNode.Index"/>,
/// e.g. <c>"42"</c>. For any other node the id is
/// <c>"&lt;nearestTimedNodeAncestorIndex&gt;/&lt;ord&gt;.&lt;ord&gt;..."</c>,
/// where each ordinal is the child index walking down from that ancestor.
/// E.g. <c>"42/3.7"</c> = child 7 of child 3 of <c>Index == 42</c>.
/// </para>
/// <para>
/// <b>Scope:</b> ids are derived from deserialization order, so they are
/// stable for the same binlog file bytes (including across
/// <c>reload_binlog</c> against the same file) but are NOT portable across
/// different binlog files. Once a binlog is overwritten by a new build,
/// discard previously returned ids.
/// </para>
/// </summary>
public static class NodeId
{
    public static string Get(BaseNode node)
    {
        if (node is TimedNode timed)
        {
            return timed.Index.ToString(CultureInfo.InvariantCulture);
        }

        // Walk up to the nearest TimedNode ancestor, recording the child
        // ordinal taken at each step.
        var ordinals = new List<int>();
        var current = node;
        while (current is not TimedNode && current.Parent is TreeNode parent)
        {
            int ordinal = parent.Children.IndexOf(current);
            if (ordinal < 0)
            {
                return null;
            }

            ordinals.Add(ordinal);
            current = parent;
        }

        if (current is not TimedNode anchor)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.Append(anchor.Index.ToString(CultureInfo.InvariantCulture));
        sb.Append('/');
        for (int i = ordinals.Count - 1; i >= 0; i--)
        {
            sb.Append(ordinals[i].ToString(CultureInfo.InvariantCulture));
            if (i > 0)
            {
                sb.Append('.');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Resolves a node id (as produced by <see cref="Get"/>) back to a
    /// <see cref="BaseNode"/>. Throws if the id cannot be parsed or the
    /// referenced node does not exist.
    /// </summary>
    public static BaseNode Resolve(LoadedBinlog entry, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Node id is empty.", nameof(id));
        }

        int slash = id.IndexOf('/');
        string indexPart = slash < 0 ? id : id.Substring(0, slash);
        if (!int.TryParse(indexPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
        {
            throw new ArgumentException($"Invalid node id: '{id}'. Expected an integer or '<int>/<ord>.<ord>...'.", nameof(id));
        }

        var map = entry.IndexMap;
        if ((uint)index >= (uint)map.Length || map[index] is not TimedNode anchor)
        {
            throw new KeyNotFoundException($"No TimedNode with Index {index} in this build.");
        }

        if (slash < 0)
        {
            return anchor;
        }

        string tail = id.Substring(slash + 1);
        if (tail.Length == 0)
        {
            return anchor;
        }

        BaseNode current = anchor;
        foreach (var ordinalText in tail.Split('.'))
        {
            if (!int.TryParse(ordinalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ordinal))
            {
                throw new ArgumentException($"Invalid node id: '{id}'. Ordinal '{ordinalText}' is not an integer.", nameof(id));
            }

            if (current is not TreeNode parent || ordinal < 0 || ordinal >= parent.Children.Count)
            {
                throw new KeyNotFoundException($"Node id '{id}' does not resolve: ordinal {ordinal} is out of range.");
            }

            current = parent.Children[ordinal];
        }

        return current;
    }
}

