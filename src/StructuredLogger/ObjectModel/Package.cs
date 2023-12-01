namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Package : NamedNode
    {
        public override string TypeName => nameof(Package);

        public string Version { get; set; }
    }
}