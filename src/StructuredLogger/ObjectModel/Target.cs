using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Target : TimedNode, IHasSourceFile, IHasRelevance
    {
        public bool Succeeded { get; internal set; }
        public string DependsOnTargets { get; set; }
        public Project Project => GetNearestParent<Project>();
        public string SourceFilePath { get; set; }
        public string ParentTarget { get; set; }
        public TargetBuiltReason TargetBuiltReason { get; set; }
        public TimedNode OriginalNode { get; set; }
        public bool Skipped { get; set; }

        public override string TypeName => nameof(Target);

        public string ParentTargetTooltip
        {
            get
            {
                if (string.IsNullOrEmpty(ParentTarget))
                {
                    return string.Empty;
                }

                if (OriginalNode != null)
                {
                    return "Navigate to where the target was built originally";
                }

                if (TargetBuiltReason == TargetBuiltReason.None)
                {
                    return $"{Name} was built because of {ParentTarget}";
                }

                var cause = TargetBuiltReason switch
                {
                    TargetBuiltReason.AfterTargets => $"Target '{Name}' had AfterTargets='{ParentTarget}' directly or indirectly",
                    TargetBuiltReason.BeforeTargets => $"Target '{Name}' had BeforeTargets='{ParentTarget}'",
                    TargetBuiltReason.DependsOn => $"Target '{ParentTarget}' had DependsOnTargets='{Name}'",
                    _ => ""
                };

                return cause;
            }
        }

        public string ParentTargetText
        {
            get
            {
                if (string.IsNullOrEmpty(ParentTarget))
                {
                    return string.Empty;
                }

                if (OriginalNode != null)
                {
                    return ParentTarget;
                }

                var connectingSymbol = TargetBuiltReason switch
                {
                    TargetBuiltReason.AfterTargets => "↑",
                    TargetBuiltReason.BeforeTargets => "↓",
                    TargetBuiltReason.DependsOn => "→",
                    _ => "→",
                };
                return $" {connectingSymbol} " + ParentTarget;
            }
        }

        public Target()
        {
            Id = -1;
        }

        public void TryAddTarget(Target target)
        {
            if (target.Parent == null)
            {
                AddChild(target);
            }
        }

        private Dictionary<int, Task> tasksById;

        public Task GetTaskById(int taskId)
        {
            if (tasksById == null)
            {
                tasksById = new Dictionary<int, Task>();
            }

            if (!tasksById.TryGetValue(taskId, out var task))
            {
                var children = Children;
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] is Task t && t.Id == taskId)
                    {
                        task = t;
                        break;
                    }
                }

                if (task != null)
                {
                    tasksById[taskId] = task;
                }
            }

            return task;
        }

        public override string ToString()
        {
            return $"Target Name={Name} Project={Path.GetFileName(Project?.ProjectFile)}";
        }

        public bool IsLowRelevance
        {
            get => HasFlag(NodeFlags.LowRelevance) && !IsSelected;
            set => SetFlag(NodeFlags.LowRelevance, value);
        }
    }
}
