namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an item/property with associated metadata (if any).
    /// </summary>
    public class Item : LogProcessNode
    {
        public string Text { get; set; }
    }
}
