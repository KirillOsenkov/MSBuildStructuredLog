namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Parameter : NamedNode
    {
        public override string TypeName => nameof(Parameter);

        public string ParameterName { get; set; }
    }
}
