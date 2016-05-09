namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NameValueNode : NamedNode
    {
        public string Value { get; set; }
        public string NameAndEquals => Name + " = ";
        public override string ToString() => Name + " = " + Value;
    }
}
