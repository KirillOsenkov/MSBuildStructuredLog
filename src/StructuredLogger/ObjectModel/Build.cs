using System.Threading;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an MSBuild overall build execution.
    /// </summary>
    public class Build : TimedNode
    {
        public StringCache StringTable { get; } = new StringCache();

        public bool IsAnalyzed { get; set; }
        public bool Succeeded { get; set; }

        public string LogFilePath { get; set; }
        public byte[] SourceFilesArchive { get; set; }

        public BuildStatistics Statistics { get; set; } = new BuildStatistics();

        public override string TypeName => nameof(Build);

        public override string ToString() => $"Build {(Succeeded ? "succeeded" : "failed")}. Duration: {this.DurationText}";

        public TreeNode FindDescendant(int index)
        {
            int current = 0;
            var cts = new CancellationTokenSource();
            TreeNode found = default;
            VisitAllChildren<TimedNode>(node =>
            {
                if (current == index)
                {
                    found = node;
                    cts.Cancel();
                }
                current++;
            }, cts.Token);

            return found;
        }
    }
}
