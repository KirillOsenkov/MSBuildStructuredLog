namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Metadata : NamedNode
    {
        public string Value { get; set; }

        public string NameAndEquals => Name + " = ";
        public override string ToString() => Name + " = " + Value;
    }
}
