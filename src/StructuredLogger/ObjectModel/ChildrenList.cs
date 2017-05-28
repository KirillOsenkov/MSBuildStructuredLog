using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ChildrenList : List<object>
    {
        public ChildrenList() : base(1)
        {
        }

        private Dictionary<ChildrenCacheKey, object> childrenCache;

        public T GetOrCreateNodeWithName<T>(string name) where T : NamedNode, new()
        {
            EnsureCacheCreated();

            var key = new ChildrenCacheKey(typeof(T), name);
            if (!childrenCache.TryGetValue(key, out var result))
            {
                for (int i = 0; i < Count; i++)
                {
                    if (this[i] is T t && t.Name == name)
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
                childrenCache = new Dictionary<ChildrenCacheKey, object>();
            }
        }

        public void OnAdded(NamedNode child)
        {
            if (child?.Name == null)
            {
                return;
            }

            EnsureCacheCreated();

            var key = new ChildrenCacheKey(child.GetType(), child.Name);
            childrenCache[key] = child;
        }

        private struct ChildrenCacheKey
        {
            private readonly Type _type;
            private readonly string _name;

            public ChildrenCacheKey(Type type, string name)
            {
                _type = type;
                _name = name;
            }

            public bool Equals(ChildrenCacheKey other)
            {
                return _type == other._type && _name == other._name;
            }

            public override bool Equals(object obj)
            {
                return obj is ChildrenCacheKey && Equals((ChildrenCacheKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_type.GetHashCode() * 397) ^ _name.GetHashCode();
                }
            }
        }
    }
}
