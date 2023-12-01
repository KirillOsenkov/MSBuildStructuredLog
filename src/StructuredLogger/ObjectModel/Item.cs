namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an item/property with associated metadata (if any).
    /// </summary>
    public class Item : NamedNode
    {
        public Item()
        {
            DisableChildrenCache = true;
        }

        public override string TypeName => nameof(Item);

        public string Text
        {
            get => Name;
            set => Name = value;
        }
    }
}
