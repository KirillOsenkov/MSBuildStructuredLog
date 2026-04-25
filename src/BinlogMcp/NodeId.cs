using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogMcp
{
    /// <summary>
    /// Stable, round-trippable identifier for any <see cref="BaseNode"/> in a
    /// loaded build.
    /// <para>
    /// For a <see cref="TimedNode"/> the id is simply its <see cref="TimedNode.Index"/>,
    /// e.g. <c>"42"</c>. For any other node the id is
    /// <c>"&lt;nearestTimedNodeAncestorIndex&gt;/&lt;ordinal&gt;.&lt;ordinal&gt;..."</c>,
    /// where each ordinal is the index of the next child to follow walking down
    /// from that ancestor. E.g. <c>"42/3.7"</c> means child 7 of child 3 of the
    /// <see cref="TimedNode"/> with <c>Index == 42</c>.
    /// </para>
    /// </summary>
    public static class NodeId
    {
        public static string Get(BaseNode node)
        {
            if (node is TimedNode timed)
            {
                return timed.Index.ToString();
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
            sb.Append(anchor.Index);
            sb.Append('/');
            for (int i = ordinals.Count - 1; i >= 0; i--)
            {
                sb.Append(ordinals[i]);
                if (i > 0)
                {
                    sb.Append('.');
                }
            }

            return sb.ToString();
        }
    }
}
