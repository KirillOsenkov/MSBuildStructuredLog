using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ParentedNode : BaseNode
    {
        private TreeNode parent;
        public TreeNode Parent
        {
            get => parent;
            set
            {
#if DEBUG
                if (parent != null && value != null)
                {
                    throw new System.InvalidOperationException("A node is being reparented");
                }
#endif

                parent = value;
            }
        }

        public IEnumerable<ParentedNode> GetParentChainExcludingThis()
        {
            var chain = new List<ParentedNode>();
            ParentedNode current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
                chain.Add(current);
            }

            chain.Reverse();
            return chain;
        }

        public IEnumerable<ParentedNode> GetParentChainIncludingThis()
        {
            var chain = new List<ParentedNode>();
            ParentedNode current = this;
            while (current.Parent != null)
            {
                chain.Add(current);
                current = current.Parent;
            }

            chain.Reverse();
            return chain;
        }

        public T GetNearestParent<T>() where T : ParentedNode
        {
            ParentedNode current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
                if (current is T)
                {
                    return (T)current;
                }
            }

            return null;
        }

        public IEnumerable<ParentedNode> EnumerateSiblingsCycle()
        {
            var parent = this.Parent;
            if (parent == null)
            {
                yield return this;
                yield break;
            }

            var index = parent.FindChildIndex(this);
            for (int i = index; i < parent.Children.Count; i++)
            {
                if (parent.Children[i] is ParentedNode child)
                {
                    yield return child;
                }
            }

            for (int i = 0; i < index; i++)
            {
                if (parent.Children[i] is ParentedNode child)
                {
                    yield return child;
                }
            }
        }
    }
}
