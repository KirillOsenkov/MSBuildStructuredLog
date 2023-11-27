using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public abstract class BaseNode : ObservableObject
    {
        private TreeNode parent;
        private NodeFlags flags;

        public TreeNode Parent
        {
            get => parent;
            set
            {
#if DEBUG
                //if (parent != null && value != null)
                //{
                //    throw new System.InvalidOperationException("A node is being reparented");
                //}
#endif

                parent = value;
            }
        }

        public virtual string TypeName => nameof(BaseNode);

        public virtual string Title => ToString();

        /// <summary>
        /// Since there can only be 1 selected node at a time, don't waste an instance field
        /// just to store a bit. Store the currently selected node here and this way we save
        /// 4 bytes per instance (due to layout/alignment). This is huge savings for large 
        /// trees.
        /// </summary>
        private static BaseNode selectedNode = null;

        public static void ClearSelectedNode()
        {
            selectedNode = null;
        }

        public bool IsSelected
        {
            get => selectedNode == this;
            set
            {
                if (IsSelected == value)
                {
                    RaisePropertyChanged();
                    return;
                }

                selectedNode = value && IsSelectable ? this : null;

                RaisePropertyChanged();
                RaisePropertyChanged("IsLowRelevance");
            }
        }

        protected virtual bool IsSelectable => true;

        public bool IsSearchResult
        {
            get => HasFlag(NodeFlags.SearchResult);
            set => SetFlag(NodeFlags.SearchResult, value);
        }

        public bool ContainsSearchResult
        {
            get => HasFlag(NodeFlags.ContainsSearchResult);
            set => SetFlag(NodeFlags.ContainsSearchResult, value);
        }

        public void ResetSearchResultStatus()
        {
            NodeFlags searchFlags = NodeFlags.SearchResult | NodeFlags.ContainsSearchResult;
            if ((flags & searchFlags) == 0)
            {
                return;
            }

            flags = flags & ~searchFlags;
            RaisePropertyChanged(nameof(IsSearchResult));
            RaisePropertyChanged(nameof(ContainsSearchResult));
        }

        private protected bool HasFlag(NodeFlags flag) => (flags & flag) == flag;

        private protected void SetFlag(NodeFlags flag, bool isSet, [CallerMemberName] string propertyName = null)
        {
            var newFlags = isSet
                ? flags | flag
                : flags & ~flag;

            if (flags == newFlags)
            {
                return;
            }

            flags = newFlags;
            RaisePropertyChanged(propertyName);
        }

        public BaseNode GetRoot()
        {
            BaseNode current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
            }

            return current;
        }

        public IEnumerable<BaseNode> GetParentChainExcludingThis()
        {
            var chain = new List<BaseNode>();
            BaseNode current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
                chain.Add(current);
            }

            chain.Reverse();
            return chain;
        }

        public IEnumerable<BaseNode> GetParentChainIncludingThis()
        {
            var chain = new List<BaseNode>();
            BaseNode current = this;
            while (current.Parent != null)
            {
                chain.Add(current);
                current = current.Parent;
            }

            chain.Reverse();
            return chain;
        }

        public T GetNearestParent<T>(Predicate<T> predicate = null) where T : BaseNode
        {
            BaseNode current = this;
            while (current.Parent != null)
            {
                current = current.Parent;
                if (current is T t && (predicate == null || predicate(t)))
                {
                    return t;
                }
            }

            return null;
        }

        public T GetNearestParentOrSelf<T>() where T : BaseNode
        {
            BaseNode current = this;
            do
            {
                if (current is T typedCurrent)
                {
                    return typedCurrent;
                }

                current = current.Parent;
            } while (current != null);

            return null;
        }

        public IEnumerable<BaseNode> EnumerateSiblingsCycle()
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
                yield return parent.Children[i];
            }

            for (int i = 0; i < index; i++)
            {
                yield return parent.Children[i];
            }
        }
    }
}
