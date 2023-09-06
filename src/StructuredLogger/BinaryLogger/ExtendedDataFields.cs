using System.Collections.Generic;

namespace Microsoft.Build.Logging;

internal class ExtendedDataFields
{
    public string ExtendedType { get; set; }
    public IDictionary<string, string> ExtendedMetadata { get; set; }
    public string ExtendedData { get; set; }
}
