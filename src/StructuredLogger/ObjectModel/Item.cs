namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an item/property with associated metadata (if any).
    /// </summary>
    public class Item : TextNode
    {
        public string NameAndEquals => string.IsNullOrWhiteSpace(Name) ? "" : Name + " = ";

        public override string TypeName => nameof(Item);

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? Text : NameAndEquals + Text;
        }
    }
}
