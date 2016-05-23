using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public abstract class TreeNode : ParentedNode
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

        private IList<object> children;
        public bool HasChildren => children != null && children.Count > 0;

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

        public void AddChildAtBeginning(object child)
        {
            if (children == null)
            {
                children = new List<object>(1);
            }

            children.Insert(0, child);

            var treeNode = child as ParentedNode;
            if (treeNode != null)
            {
                treeNode.Parent = this;
            }

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

            var treeNode = child as ParentedNode;
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

        public virtual T FindFirstInSubtreeIncludingSelf<T>(Predicate<T> predicate = null)
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
                    else if (child is T && (predicate == null || predicate((T)child)))
                    {
                        return (T)child;
                    }
                }
            }

            return default(T);
        }

        public virtual T FindLastInSubtreeIncludingSelf<T>(Predicate<T> predicate = null)
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
                    else if (child is T && (predicate == null || predicate((T)child)))
                    {
                        return (T)child;
                    }
                }
            }

            return default(T);
        }

        public int FindChildIndex(object child)
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

        public T FindPreviousChild<T>(object currentChild, Predicate<T> predicate = null)
        {
            var i = FindChildIndex(currentChild);
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

        public T FindNextChild<T>(object currentChild, Predicate<T> predicate = null)
        {
            var i = FindChildIndex(currentChild);
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

        public T FindPreviousInTraversalOrder<T>(Predicate<T> predicate = null)
        {
            if (Parent == null)
            {
                return default(T);
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
                    return default(T);
                }
            }

            if (Parent is T && (predicate == null || predicate((T)(object)Parent)))
            {
                return (T)(object)Parent;
            }

            return Parent.FindPreviousInTraversalOrder<T>(predicate);
        }

        public T FindNextInTraversalOrder<T>(Predicate<T> predicate = null)
        {
            if (Parent == null)
            {
                return default(T);
            }

            var current = Parent.FindNextChild<T>(this);

            while (current != null)
            {
                T first = current;

                var treeNode = current as TreeNode;
                if (treeNode != null)
                {
                    first = treeNode.FindFirstInSubtreeIncludingSelf<T>(predicate);
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
                    return default(T);
                }
            }

            if (Parent != null)
            {
                return Parent.FindNextInTraversalOrder<T>(predicate);
            }

            return default(T);
        }

        public void VisitAllChildren<T>(Action<T> processor, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (this is T)
            {
                processor((T)(object)this);
            }

            if (HasChildren)
            {
                foreach (var child in Children)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var node = child as TreeNode;
                    if (node != null)
                    {
                        node.VisitAllChildren<T>(processor, cancellationToken);
                    }
                    else if (child is T)
                    {
                        processor((T)child);
                    }
                }
            }
        }
    }
}
