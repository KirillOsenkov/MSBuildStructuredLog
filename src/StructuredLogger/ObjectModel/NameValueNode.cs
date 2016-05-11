namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NameValueNode : BaseNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string NameAndEquals => Name + " = ";
        public override string ToString() => Name + " = " + Value;
        public bool IsVisible { get { return true; } set { } }
        public bool IsExpanded { get { return true; } set { } }
    }
}
