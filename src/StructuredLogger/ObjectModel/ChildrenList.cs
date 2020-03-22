using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ChildrenList : List<BaseNode>
    {
        public ChildrenList() : base(1)
        {
        }

        public ChildrenList(IEnumerable<BaseNode> children) : base(children)
        {
        }

        private Dictionary<ChildrenCacheKey, BaseNode> childrenCache;

        public T FindNode<T>(string name) where T : NamedNode
        {
            EnsureCacheCreated();

            var key = new ChildrenCacheKey(typeof(T), name);
            if (!childrenCache.TryGetValue(key, out var result))
            {
                for (int i = 0; i < Count; i++)
                {
                    if (this[i] is T t && t.LookupKey == name)
                    {
                        childrenCache[key] = t;
                        return t;
                    }
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

        public void OnAdded(NamedNode child)
        {
            if (child?.LookupKey == null)
            {
                return;
            }

            EnsureCacheCreated();

            var key = new ChildrenCacheKey(child.GetType(), child.LookupKey);
            childrenCache[key] = child;
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
                hashCode = unchecked((_type.GetHashCode() * 397) ^ _name.ToLowerInvariant().GetHashCode());
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
