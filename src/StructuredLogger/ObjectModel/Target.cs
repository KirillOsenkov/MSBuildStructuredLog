using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Target : TimedNode
    {
        public bool Succeeded { get; internal set; }
        public string DependsOnTargets { get; set; }
        public Project Project => GetNearestParent<Project>();

        public Target()
        {
            Id = -1;
        }

        public Task GetTaskById(int taskId)
        {
            return Children.OfType<Task>().First(t => t.Id == taskId);
        }

        public override string ToString()
        {
            return $"Target Id={Id} Name={Name}";
        }
    }
}
