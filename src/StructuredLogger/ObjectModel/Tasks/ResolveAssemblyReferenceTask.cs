namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ResolveAssemblyReferenceTask : Task
    {
        //public override string TypeName => nameof(ResolveAssemblyReferenceTask);

        public Folder Inputs { get; set; }
        public Folder Results { get; set; }
    }
}
