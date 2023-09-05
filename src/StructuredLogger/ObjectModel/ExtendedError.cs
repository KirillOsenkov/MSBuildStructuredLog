using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger;

public class ExtendedError : Error, IHasExtendedData
{
    public ExtendedError(string extendedType, IDictionary<string, string>? extendedMetadata, string extendedData)
    {
        ExtendedType = extendedType;
        ExtendedMetadata = extendedMetadata;
        ExtendedData = extendedData;
    }

    public string ExtendedType { get; set; }
    public IDictionary<string, string>? ExtendedMetadata { get; set; }
    public string? ExtendedData { get; set; }
}
