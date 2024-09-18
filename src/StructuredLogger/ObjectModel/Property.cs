namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Property : NameValueNode
    {
        public override string TypeName => nameof(Property);
    }

    public class TaskParameterProperty : Property
    {
        public string ParameterName { get; set; }
    }
}
