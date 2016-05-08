namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an item/property with associated metadata (if any).
    /// </summary>
    public class Item : LogProcessNode
    {
        public string ItemSpec { get; set; }
        public string NameAndEquals => string.IsNullOrWhiteSpace(Name) ? "" : Name + " = ";
    }
}
