namespace Microsoft.Build.Logging.StructuredLogger
{
    public class AddItem : NamedNode, IHasLineNumber
    {
        public override string TypeName => nameof(AddItem);

        public int? LineNumber { get; set; }
    }
}
