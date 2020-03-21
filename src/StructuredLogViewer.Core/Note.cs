namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Note : TextNode
    {
        protected override bool IsSelectable => false;

        public override string TypeName => nameof(Note);
    }
}
