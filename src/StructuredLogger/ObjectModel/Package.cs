namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Package : NamedNode
    {
        public override string TypeName => nameof(Package);

        public string Version { get; set; }

        public override string ToString()
        {
            string result = Name;
            if (!string.IsNullOrEmpty(Version))
            {
                result = $"{result} {Version}";
            }

            return result;
        }
    }
}