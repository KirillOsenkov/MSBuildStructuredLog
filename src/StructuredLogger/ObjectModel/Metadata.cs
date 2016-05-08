namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Metadata : LogProcessNode
    {
        public string Value { get; set; }

        public string NameAndEquals => Name + " = ";
        public override string ToString() => Name + " = " + Value;
    }
}
