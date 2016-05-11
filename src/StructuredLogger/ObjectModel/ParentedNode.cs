using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ParentedNode : BaseNode
    {
        public TreeNode Parent { get; set; }

        public IEnumerable<ParentedNode> GetParentChain()
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
