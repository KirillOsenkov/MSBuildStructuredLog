using System.IO;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Target : TimedNode, IHasSourceFile, IHasRelevance
    {
        public bool Succeeded { get; internal set; }
        public string DependsOnTargets { get; set; }
        public Project Project => GetNearestParent<Project>();
        public string SourceFilePath { get; internal set; }

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
    }
}
