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

        public byte[] SourceFilesArchive { get; set; }

        public override string ToString() => "Build " + (Succeeded ? "succeeded" : "failed");
    }
}
