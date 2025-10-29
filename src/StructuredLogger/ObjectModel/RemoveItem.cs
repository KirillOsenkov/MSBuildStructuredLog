namespace Microsoft.Build.Logging.StructuredLogger
{
    public class RemoveItem : AddOrRemoveItem, IHasLineNumber
    {
        public RemoveItem()
        {
            DisableChildrenCache = true;
        }

        public override string TypeName => nameof(RemoveItem);

        public int? LineNumber { get; set; }
    }
}
