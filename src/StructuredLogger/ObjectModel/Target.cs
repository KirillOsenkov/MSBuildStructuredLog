using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an MSBuild target execution.
    /// </summary>
    public class Target : LogProcessNode
    {
        public bool Succeeded { get; internal set; }

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
