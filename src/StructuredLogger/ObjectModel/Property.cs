using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Property : NameValueNode
    {
        public Property()
        {
        }

        public Property(KeyValuePair<string, string> kvp)
        {
            Name = kvp.Key;
            Value = kvp.Value;
        }
    }
}
