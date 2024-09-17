using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public bool DisableChildrenCache
        {
            get => HasFlag(NodeFlags.DisableChildrenCache);
            set => SetFlag(NodeFlags.DisableChildrenCache, value);
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
                    children = CreateChildrenList();
                }

                return children;
            }
        }

        protected ChildrenList CreateChildrenList()
        {
            if (DisableChildrenCache)
            {
                return new ChildrenList();
            }

            return new CacheByNameChildrenList();
        }

        protected ChildrenList CreateChildrenList(int capacity)
        {
            if (DisableChildrenCache)
            {
                return new ChildrenList(capacity);
            }

            return new CacheByNameChildrenList(capacity);
        }

        protected ChildrenList CreateChildrenList(IEnumerable<BaseNode> children)
        {
            if (DisableChildrenCache)
            {
                return new ChildrenList(children);
            }

            return new CacheByNameChildrenList(children);
        }

        public void EnsureChildrenCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                return;
            }

            if (children == null)
            {
                children = CreateChildrenList(capacity);
            }
            else if (children is ChildrenList list)
            {
                list.EnsureCapacity(list.Count + capacity);
            }
        }

        private static int CompareByToString(BaseNode o1, BaseNode o2)
            => string.Compare(o1.ToString(), o2.ToString(), StringComparison.OrdinalIgnoreCase);

        public void SortChildren(Comparison<BaseNode> comparison = null)
        {
            if (children == null || children.Count < 2)
            {
                return;
            }

            comparison ??= CompareByToString;

            if (children is not ChildrenList list)
            {
                list = CreateChildrenList(children);
            }

            list.Sort(comparison);
            if (list != children)
            {
                children = list.ToArray();
                RaisePropertyChanged(nameof(HasChildren));
                RaisePropertyChanged(nameof(Children));
            }
            else
            {
                list.RaiseCollectionChanged();
            }
        }

        public void MakeChildrenObservable()
        {
            if (children is ObservableCollection<BaseNode>)
            {
                return;
            }

            if (children == null)
            {
                children = new ObservableCollection<BaseNode>();
            }
            else
            {
                children = new ObservableCollection<BaseNode>(children);
            }

            RaisePropertyChanged(nameof(HasChildren));
            RaisePropertyChanged(nameof(Children));
        }

        public void AddChildAtBeginning(BaseNode child)
        {
            if (children == null)
            {
                children = CreateChildrenList(1);
            }

            children.Insert(0, child);

            child.Parent = this;
        }

        public virtual void AddChild(BaseNode child)
        {
            if (children == null)
            {
                children = CreateChildrenList(1);
            }

            children.Add(child);

            child.Parent = this;
        }

        public T GetOrCreateNodeWithText<T>(string text, bool addAtBeginning = false) where T : TextNode, new()
        {
            T node = FindChild<T>(text);
            if (node != null)
            {
                return node;
            }

            var newNode = new T() { Text = text };
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

        public virtual T FindChild<T>(string name) where T : BaseNode
        {
            if (Children is ChildrenList list)
            {
                return list.FindNode<T>(name);
            }

            return FindChild<T, string>(static (c, name) => string.Equals(c.Title, name, StringComparison.OrdinalIgnoreCase), name);
        }

        public virtual T FindChild<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            if (HasChildren)
            {
                var children = Children;
                int count = children.Count;
                for (int i = 0; i < count; i++)
                {
                    if (children[i] is T child && (predicate == null || predicate(child)))
                    {
                        return child;
                    }
                }
            }

            return default;
        }

        public virtual T FindChild<T, TState>(Func<T, TState, bool> predicate, TState state) where T : BaseNode
        {
            if (HasChildren)
            {
                var children = Children;
                int count = children.Count;
                for (int i = 0; i < count; i++)
                {
                    if (children[i] is T child && predicate(child, state))
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
                var children = Children;
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
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
                var children = Children;
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];

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

        public virtual T FindLastInSubtreeIncludingSelf<T>(Predicate<T> predicate = null) where T : BaseNode
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
                IList<BaseNode> children = Children;
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    if (children[i] is T child && (predicate == null || predicate(child)))
                    {
                        return child;
                    }
                }
            }

            return default;
        }

        public virtual T FindLastChild<T, TState>(Func<T, TState, bool> predicate, TState state) where T : BaseNode
        {
            if (HasChildren)
            {
                IList<BaseNode> children = Children;
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    if (children[i] is T child && predicate(child, state))
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

        /// <summary>
        /// Iterate through all the children nodes in parallel.  Make sure the action is threadsafe.
        /// </summary>
        public void ParallelVisitAllChildren<T>(
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

                void ProcessChild(BaseNode child)
                {
                    if (child is TreeNode node)
                    {
                        node.ParallelVisitAllChildren(processor, cancellationToken, takeChildrenSnapshot);
                    }
                    else if (child is T typedChild)
                    {
                        processor(typedChild);
                    }
                }

                // A short list is faster on a single thread.
                // Need more testing to determine the cut off.
                if (list.Count < Environment.ProcessorCount * 3)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        ProcessChild(list[i]);
                    }
                }
                else
                {
                    ParallelOptions po = new ParallelOptions() { CancellationToken = cancellationToken };
                    Parallel.ForEach(list, po, child => ProcessChild(child));
                }
            }
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

                for (int i = 0; i < list.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var child = list[i];
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

        // A breadth first search looking for the first node of type T.
        public IReadOnlyList<T> FindImmediateChildrenOfType<T>()
        {
            Queue<BaseNode> searchQueue = new Queue<BaseNode>(Children);
            List<T> resultNodes = new List<T>();

            while (searchQueue.Count > 0)
            {
                var node = searchQueue.Dequeue();
                if (node is T tNode)
                {
                    resultNodes.Add(tNode);
                }
                else if (node is TreeNode treeNode && treeNode.HasChildren)
                {
                    foreach (BaseNode child in treeNode.Children)
                    {
                        searchQueue.Enqueue(child);
                    }
                }
            }

            return resultNodes;
        }
    }
}
