using System.Collections.Generic;

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
                if (parent != null)
                {
                    throw new System.InvalidOperationException("A node is being reparented");
                }
#endif

                parent = value;
            }
        }

        public IEnumerable<ParentedNode> GetParentChain()
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
    }
}
