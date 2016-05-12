namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NameValueNode : ParentedNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string NameAndEquals => Name + " = ";
        public string ShortenedValue => Utilities.ShortenValue(Value);

        public override string ToString() => Name + " = " + Value;
        public bool IsVisible { get { return true; } set { } }
        public bool IsExpanded { get { return true; } set { } }
    }
}
