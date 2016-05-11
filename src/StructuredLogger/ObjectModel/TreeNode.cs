using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public abstract class TreeNode : BaseNode
    {
        private bool isVisible = true;
        public bool IsVisible
        {
            get
            {
                return isVisible;
            }

            set
            {
                if (isVisible == value)
                {
                    return;
                }

                isVisible = value;
                RaisePropertyChanged();
            }
        }

        private bool isExpanded = false;
        public bool IsExpanded
        {
            get
            {
                return isExpanded;
            }

            set
            {
                if (isExpanded == value)
                {
                    return;
                }

                isExpanded = value;
                RaisePropertyChanged();
            }
        }

        public TreeNode Parent { get; set; }
        private IList<object> children;
        public bool HasChildren => children != null && children.Count > 0;

        public IEnumerable<TreeNode> GetParentChain()
        {
            var chain = new List<TreeNode>();
            TreeNode current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
                chain.Add(current);
            }

            chain.Reverse();
            return chain;
        }

        public T GetNearestParent<T>() where T : TreeNode
        {
            TreeNode current = this;
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

        public IList<object> Children
        {
            get
            {
                if (children == null)
                {
                    children = new List<object>(1);
                }

                return children;
            }
        }

        public void Seal()
        {
            if (children != null)
            {
                children = children.ToArray();
            }
        }

        public void AddChildAtBeginning(TreeNode child)
        {
            if (children == null)
            {
                children = new List<object>(1);
            }

            children.Insert(0, child);
            child.Parent = this;

            if (children.Count == 1)
            {
                RaisePropertyChanged(nameof(HasChildren));
            }
        }

        public virtual void AddChild(object child)
        {
            if (children == null)
            {
                children = new List<object>(1);
            }

            children.Add(child);
            var treeNode = child as TreeNode;
            if (treeNode != null)
            {
                treeNode.Parent = this;
            }

            if (children.Count == 1)
            {
                RaisePropertyChanged(nameof(HasChildren));
            }
        }

        public T GetOrCreateNodeWithName<T>(string name) where T : NamedNode, new()
        {
            var existing = FindChild<T>(n => n.Name == name);
            if (existing == null)
            {
                existing = new T() { Name = name };
                this.AddChild(existing);
            }

            return existing;
        }

        public virtual T FindChild<T>(Predicate<T> predicate = null)
        {
            if (HasChildren)
            {
                foreach (var child in Children.OfType<T>())
                {
                    if (predicate == null || predicate(child))
                    {
                        return child;
                    }
                }
            }

            return default(T);
        }

        public virtual T FindFirstInSubtree<T>(Predicate<T> predicate = null)
        {
            if (this is T && (predicate == null || predicate((T)(object)this)))
            {
                return (T)(object)this;
            }

            return FindFirstChild<T>(predicate);
        }

        public virtual T FindFirstChild<T>(Predicate<T> predicate = null)
        {
            if (HasChildren)
            {
                foreach (var child in Children.OfType<TreeNode>())
                {
                    var found = child.FindFirstInSubtree<T>(predicate);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return default(T);
        }

        public virtual T FindLastInSubtree<T>(Predicate<T> predicate = null)
        {
            var child = FindLastChild<T>(predicate);
            if (child != null)
            {
                return child;
            }

            if (this is T && (predicate == null || predicate((T)(object)this)))
            {
                return (T)(object)this;
            }

            return default(T);
        }

        public virtual T FindLastChild<T>(Predicate<T> predicate = null)
        {
            if (HasChildren)
            {
                foreach (var child in Children.OfType<TreeNode>().Reverse())
                {
                    var found = child.FindLastInSubtree<T>(predicate);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return default(T);
        }

        public int FindIndex(TreeNode child)
        {
            if (HasChildren)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i] == child)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        public T FindPrevious<T>(TreeNode child, Predicate<T> predicate = null)
        {
            var i = FindIndex(child);
            if (i == -1)
            {
                return default(T);
            }

            for (int j = i - 1; j >= 0; j--)
            {
                if (Children[j] is T && (predicate == null || predicate((T)Children[j])))
                {
                    return (T)Children[j];
                }
            }

            return default(T);
        }

        public T FindNext<T>(TreeNode child, Predicate<T> predicate = null)
        {
            var i = FindIndex(child);
            if (i == -1)
            {
                return default(T);
            }

            for (int j = i + 1; j < Children.Count; j++)
            {
                if (Children[j] is T && (predicate == null || predicate((T)Children[j])))
                {
                    return (T)Children[j];
                }
            }

            return default(T);
        }

        public T FindPrevious<T>(Predicate<T> predicate = null)
        {
            if (Parent == null)
            {
                return default(T);
            }

            return Parent.FindPrevious<T>(this, predicate);
        }

        public T FindNext<T>(Predicate<T> predicate = null)
        {
            if (Parent == null)
            {
                return default(T);
            }

            return Parent.FindNext<T>(this, predicate);
        }

        public T FindPreviousInTraversalOrder<T>(Predicate<T> predicate = null)
        {
            var current = FindPrevious<TreeNode>() as TreeNode;

            while (current != null)
            {
                var last = current.FindLastInSubtree<T>(predicate);
                if (last != null)
                {
                    return last;
                }

                if (Parent != null)
                {
                    current = Parent.FindPrevious<TreeNode>(current) as TreeNode;
                }
                else
                {
                    return default(T);
                }
            }

            if (Parent != null)
            {
                if (Parent is T && (predicate == null || predicate((T)(object)Parent)))
                {
                    return (T)(object)Parent;
                }

                return Parent.FindPreviousInTraversalOrder<T>(predicate);
            }

            return default(T);
        }

        public T FindNextInTraversalOrder<T>(Predicate<T> predicate = null)
        {
            var current = FindNext<TreeNode>() as TreeNode;

            while (current != null)
            {
                var first = current.FindFirstInSubtree<T>(predicate);
                if (first != null)
                {
                    return first;
                }

                if (Parent != null)
                {
                    current = Parent.FindNext<TreeNode>(current) as TreeNode;
                }
                else
                {
                    return default(T);
                }
            }

            if (Parent != null)
            {
                return Parent.FindNextInTraversalOrder<T>(predicate);
            }

            return default(T);
        }

        public void VisitAllChildren<T>(Action<T> processor)
        {
            if (this is T)
            {
                processor((T)(object)this);
            }

            if (HasChildren)
            {
                foreach (var child in Children)
                {
                    var node = child as TreeNode;
                    if (node != null)
                    {
                        node.VisitAllChildren<T>(processor);
                    }
                }
            }
        }

        public virtual int TotalItemsInSubtree()
        {
            int sum = 1;
            if (HasChildren)
            {
                foreach (var child in Children.OfType<TreeNode>())
                {
                    sum += child.TotalItemsInSubtree();
                }
            }

            return sum;
        }

        public virtual void WriteTo(StringBuilder sb, int indent = 0)
        {
            sb.Append(new string(' ', indent * 4));
            sb.AppendLine(this.ToString());
            if (HasChildren)
            {
                foreach (var child in Children.OfType<TreeNode>())
                {
                    child.WriteTo(sb, indent + 1);
                }
            }
        }
    }
}
