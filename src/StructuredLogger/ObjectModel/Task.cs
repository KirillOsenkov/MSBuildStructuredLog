using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Task : TimedNode, IHasSourceFile, IHasLineNumber
    {
        public string FromAssembly { get; set; }
        public string CommandLineArguments { get; set; }
        public string SourceFilePath { get; set; }

        public override string TypeName => nameof(Task);

        public virtual bool IsDerivedTask => this.GetType() != typeof(Task);

        public int? LineNumber { get; set; }

        public IReadOnlyList<Message> GetMessages()
        {
            TreeNode node = this;
            if (this.FindChild<Folder>(Strings.Messages) is Folder messagesFolder)
            {
                node = messagesFolder;
            }

            return node.Children.OfType<Message>().ToArray();
        }
    }

    /// <summary>
    /// Used temporarily to parent projects to some task if the ProjectStarted event
    /// arrives before the TaskStarted of the parent MSBuild task
    /// </summary>
    internal class PlaceholderTask : Task
    {
    }

    public class MSBuildTask : Task
    {
        //public override string TypeName => nameof(MSBuildTask);
    }
}
