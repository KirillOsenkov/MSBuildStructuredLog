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
        public int TargetBuiltReason { get; set; }

        public override string TypeName => nameof(Target);

        public string ParentTargetTooltip
        {
            get
            {
                if (string.IsNullOrEmpty(ParentTarget))
                {
                    return string.Empty;
                }

                if (TargetBuiltReason == 0)
                {
                    return $"{Name} was built because of {ParentTarget}";
                }

                return "";
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
                var connectingSymbol = TargetBuiltReason switch
                {
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

        public Task GetTaskById(int taskId)
        {
            return Children.OfType<Task>().FirstOrDefault(t => t.Id == taskId);
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
