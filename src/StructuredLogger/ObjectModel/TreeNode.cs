using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public abstract class TreeNode : BaseNode
    {
        public bool IsVisible
        {
            get => !HasFlag(NodeFlags.Hidden);
            set => SetFlag(NodeFlags.Hidden, !value);
        }

        public bool IsExpanded
        {
            get => HasFlag(NodeFlags.Expanded);
            set => SetFlag(NodeFlags.Expanded, value);
        }

        public virtual string ToolTip
        {
            get => null;
        }

        private IList<BaseNode> children;
        public bool HasChildren => children != null && children.Count > 0;

        public IList<BaseNode> Children
        {
            get
            {
                if (children == null)
                {
                    children = new ChildrenList();
                }

                return children;
            }
        }

        public void SortChildren()
        {
            if (children == null)
            {
                return;
            }

            if (children is not ChildrenList list)
            {
                list = new ChildrenList(children);
            }

            list.Sort((o1, o2) => string.CompareOrdinal(o1.ToString(), o2.ToString()));
            if (list != children)
            {
                children = list.ToArray();
            }

            RaisePropertyChanged(nameof(HasChildren));
            RaisePropertyChanged(nameof(Children));
        }

        public void Seal()
        {
            if (children != null)
            {
                children = children.ToArray();
            }
        }

        public void Unseal()
        {
            if (children is BaseNode[])
            {
                children = new ChildrenList(children);
            }
        }

        public void AddChildAtBeginning(BaseNode child)
        {
            if (children == null)
            {
                children = new ChildrenList();
            }

            children.Insert(0, child);
            if (child is NamedNode named)
            {
                ((ChildrenList)children).OnAdded(named);
            }

            child.Parent = this;

            if (children.Count >= 1)
            {
                RaisePropertyChanged(nameof(HasChildren));
                RaisePropertyChanged(nameof(Children));
            }
        }

        public virtual void AddChild(BaseNode child)
        {
            if (children == null)
            {
                children = new ChildrenList();
            }

            children.Add(child);
            if (child is NamedNode named)
            {
                ((ChildrenList)children).OnAdded(named);
            }

            child.Parent = this;

            if (children.Count >= 1)
            {
                RaisePropertyChanged(nameof(HasChildren));
                RaisePropertyChanged(nameof(Children));
            }
        }

        public T GetOrCreateNodeWithName<T>(string name, bool addAtBeginning = false) where T : NamedNode, new()
        {
            T node = FindChild<T>(name);
            if (node != null)
            {
                return node;
            }

            var newNode = new T() { Name = name };
            if (addAtBeginning)
            {
                this.AddChildAtBeginning(newNode);
            }
            else
            {
                this.AddChild(newNode);
            }

            return newNode;
        }

        public virtual T FindChild<T>(string name) where T : NamedNode
        {
            if (Children is ChildrenList list)
            {
                return list.FindNode<T>(name);
            }

            return FindChild<T>(c => string.Equals(c.LookupKey, name, StringComparison.OrdinalIgnoreCase));
        }

        public virtual T FindChild<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            if (HasChildren)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i] is T child && (predicate == null || predicate(child)))
                    {
                        return child;
                    }
                }
            }

            return default;
        }

        public virtual T FindFirstInSubtreeIncludingSelf<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            if (this is T typedThis && (predicate == null || predicate(typedThis)))
            {
                return typedThis;
            }

            return FindFirstDescendant<T>(predicate);
        }

        public virtual T FindFirstChild<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            if (HasChildren)
            {
                foreach (var child in Children)
                {
                    if (child is T typedChild && (predicate == null || predicate(typedChild)))
                    {
                        return typedChild;
                    }
                }
            }

            return default;
        }

        public virtual T FindFirstDescendant<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            if (HasChildren)
            {
                foreach (var child in Children)
                {
                    var treeNode = child as TreeNode;
                    if (treeNode != null)
                    {
                        var found = treeNode.FindFirstInSubtreeIncludingSelf<T>(predicate);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                    else if (child is T typedChild && (predicate == null || predicate(typedChild)))
                    {
                        return typedChild;
                    }
                }
            }

            return default;
        }

        public virtual T FindLastInSubtreeIncludingSelf<T>(Predicate<T> predicate = null)  where T : BaseNode
        {
            var child = FindLastDescendant<T>(predicate);
            if (child != null)
            {
                return child;
            }

            if (this is T typedThis && (predicate == null || predicate(typedThis)))
            {
                return typedThis;
            }

            return default;
        }

        public virtual T FindLastChild<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            if (HasChildren)
            {
                for (int i = Children.Count - 1; i >= 0; i--)
                {
                    if (Children[i] is T child && (predicate == null || predicate(child)))
                    {
                        return child;
                    }
                }
            }

            return default;
        }

        public BaseNode FirstChild
        {
            get
            {
                if (HasChildren)
                {
                    return Children[0];
                }

                return null;
            }
        }

        public BaseNode LastChild
        {
            get
            {
                if (HasChildren)
                {
                    return Children[Children.Count - 1];
                }

                return null;
            }
        }

        public virtual T FindLastDescendant<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            if (HasChildren)
            {
                foreach (var child in Children.Reverse())
                {
                    var treeNode = child as TreeNode;
                    if (treeNode != null)
                    {
                        var found = treeNode.FindLastInSubtreeIncludingSelf(predicate);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                    else if (child is T typedChild && (predicate == null || predicate(typedChild)))
                    {
                        return typedChild;
                    }
                }
            }

            return default;
        }

        public int FindChildIndex(BaseNode child)
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

        public T FindPreviousChild<T>(BaseNode currentChild, Predicate<T> predicate = null) where T : BaseNode
        {
            var i = FindChildIndex(currentChild);
            if (i == -1)
            {
                return default;
            }

            for (int j = i - 1; j >= 0; j--)
            {
                if (Children[j] is T typedChild && (predicate == null || predicate(typedChild)))
                {
                    return typedChild;
                }
            }

            return default;
        }

        public T FindNextChild<T>(BaseNode currentChild, Predicate<T> predicate = null) where T : BaseNode
        {
            var i = FindChildIndex(currentChild);
            if (i == -1)
            {
                return default;
            }

            for (int j = i + 1; j < Children.Count; j++)
            {
                if (Children[j] is T typedChild && (predicate == null || predicate(typedChild)))
                {
                    return typedChild;
                }
            }

            return default;
        }

        public T FindPreviousInTraversalOrder<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            if (Parent == null)
            {
                return default;
            }

            var current = Parent.FindPreviousChild<T>(this);

            while (current != null)
            {
                T last = current;

                var treeNode = current as TreeNode;
                if (treeNode != null)
                {
                    last = treeNode.FindLastInSubtreeIncludingSelf<T>(predicate);
                }

                if (last != null)
                {
                    return last;
                }

                if (Parent != null)
                {
                    current = Parent.FindPreviousChild<T>(current);
                }
                else
                {
                    // no parent and no previous; we must be at the top
                    return default;
                }
            }

            if (Parent is T typedParent && (predicate == null || predicate(typedParent)))
            {
                return typedParent;
            }

            return Parent.FindPreviousInTraversalOrder(predicate);
        }

        public T FindNextInTraversalOrder<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            if (Parent == null)
            {
                return default;
            }

            var current = Parent.FindNextChild<T>(this);

            while (current != null)
            {
                T first = current;

                var treeNode = current as TreeNode;
                if (treeNode != null)
                {
                    first = treeNode.FindFirstInSubtreeIncludingSelf(predicate);
                }

                if (first != null)
                {
                    return first;
                }

                if (Parent != null)
                {
                    current = Parent.FindNextChild<T>(current);
                }
                else
                {
                    return default;
                }
            }

            if (Parent != null)
            {
                return Parent.FindNextInTraversalOrder(predicate);
            }

            return default;
        }

        public IReadOnlyList<T> FindChildrenRecursive<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            var foundChildren = new List<T>();

            VisitAllChildren<T>(
                c =>
                {
                    if (predicate == null || predicate(c))
                    {
                        foundChildren.Add(c);
                    }
                });

            return foundChildren;
        }

        public void VisitAllChildren<T>(
            Action<T> processor,
            CancellationToken cancellationToken = default,
            bool takeChildrenSnapshot = false) where T : BaseNode
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (this is T typedThis)
            {
                processor(typedThis);
            }

            if (HasChildren)
            {
                var list = Children;
                if (takeChildrenSnapshot)
                {
                    list = list.ToArray();
                }

                foreach (var child in list)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (child is TreeNode node)
                    {
                        node.VisitAllChildren(processor, cancellationToken, takeChildrenSnapshot);
                    }
                    else if (child is T typedChild)
                    {
                        processor(typedChild);
                    }
                }
            }
        }
    }
}
