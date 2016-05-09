using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Property : NamedNode
    {
        public Property()
        {
        }

        public Property(KeyValuePair<string, string> kvp)
        {
            Name = kvp.Key;
            Value = kvp.Value;
        }

        public string Value { get; set; }
        public string NameAndEquals => Name + " = ";

        public override string ToString() => Name + " = " + Value;
    }
}
