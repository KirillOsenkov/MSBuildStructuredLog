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
    }

    public class MSBuildTask : Task
    {
        //public override string TypeName => nameof(MSBuildTask);
    }
}
