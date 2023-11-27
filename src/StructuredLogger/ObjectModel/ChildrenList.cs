using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ChildrenList : List<BaseNode>, INotifyCollectionChanged
    {
        public ChildrenList() : base(0)
        {
        }

        public ChildrenList(int capacity) : base(capacity)
        {
        }

        public ChildrenList(IEnumerable<BaseNode> children) : base(children)
        {
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public void RaiseCollectionChanged()
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public virtual T FindNode<T>(string name) where T : BaseNode
        {
            for (int i = 0; i < Count; i++)
            {
                if (this[i] is T t && t.Title == name)
                {
                    return t;
                }
            }

            return default;
        }

        public void EnsureCapacity(int capacity)
        {
            this.Capacity = capacity;
        }
    }

    public class CacheByNameChildrenList : ChildrenList
    {
        private Dictionary<ChildrenCacheKey, BaseNode> childrenCache;

        public CacheByNameChildrenList() : base()
        {
        }

        public CacheByNameChildrenList(int capacity) : base(capacity)
        {
        }

        public CacheByNameChildrenList(IEnumerable<BaseNode> children) : base(children)
        {
        }

        public override T FindNode<T>(string name)
        {
            EnsureCacheCreated();

            var key = new ChildrenCacheKey(typeof(T), name);
            if (!childrenCache.TryGetValue(key, out var result))
            {
                result = base.FindNode<T>(name);
                if (result != null)
                {
                    childrenCache[key] = result;
                }
            }

            return (T)result;
        }

        private void EnsureCacheCreated()
        {
            if (childrenCache == null)
            {
                childrenCache = new Dictionary<ChildrenCacheKey, BaseNode>();
            }
        }

        private struct ChildrenCacheKey : IEquatable<ChildrenCacheKey>
        {
            private readonly Type _type;
            private readonly string _name;
            private readonly int hashCode;

            public ChildrenCacheKey(Type type, string name)
            {
                _type = type;
                _name = name;
                hashCode = unchecked((_type.GetHashCode() * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(_name));
            }

            public bool Equals(ChildrenCacheKey other)
            {
                return _type == other._type && string.Equals(_name, other._name, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is ChildrenCacheKey key && Equals(key);
            }

            public override int GetHashCode() => hashCode;
        }
    }
}
