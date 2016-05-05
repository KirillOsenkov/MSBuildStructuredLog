namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Property
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public override string ToString() => Name + " = " + Value;
    }
}
