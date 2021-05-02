namespace Microsoft.Build.Logging.StructuredLogger
{
    public class RemoveItem : NamedNode, IHasLineNumber
    {
        public override string TypeName => nameof(RemoveItem);

        public int? LineNumber { get; set; }
    }
}
