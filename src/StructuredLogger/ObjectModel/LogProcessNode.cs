using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class LogProcessNode : TreeNode
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public LogProcessNode GetOrCreateNodeWithName<T>(string name) where T : LogProcessNode, new()
        {
            var existing = FindFirst<T>(n => n.Name == name);
            if (existing == null)
            {
                existing = new T() { Name = name };
                this.AddChild(existing);
            }

            return existing;
        }

        public void AddAllChildren<T>(Predicate<T> predicate, List<T> list)
        {
            foreach (var child in Children)
            {
                if (child is T && predicate((T)(object)child))
                {
                    list.Add((T)(object)child);
                }

                var node = child as LogProcessNode;
                if (node != null)
                {
                    node.AddAllChildren(predicate, list);
                }
            }
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
                    var node = child as LogProcessNode;
                    if (node != null)
                    {
                        node.VisitAllChildren<T>(processor);
                    }
                }
            }
        }

        private bool isLowRelevance = false;
        public bool IsLowRelevance
        {
            get
            {
                return isLowRelevance && !IsSelected;
            }

            set
            {
                if (isLowRelevance == value)
                {
                    return;
                }

                isLowRelevance = value;
                RaisePropertyChanged();
            }
        }

        public IEnumerable<LogProcessNode> GetParentChain()
        {
            List<LogProcessNode> chain = new List<LogProcessNode>();
            LogProcessNode current = this;
            while (current.Parent != null)
            {
                current = current.Parent as LogProcessNode;
                chain.Add(current);
            }

            chain.Reverse();
            return chain;
        }
    }
}
