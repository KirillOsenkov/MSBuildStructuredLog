using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger;

public class ExtendedMessage : Message, IHasExtendedData
{
    public ExtendedMessage(string extendedType, IDictionary<string, string>? extendedMetadata, string extendedData)
    {
        ExtendedType = extendedType;
        ExtendedMetadata = extendedMetadata;
        ExtendedData = extendedData;
    }

    public string ExtendedType { get; }
    public IDictionary<string, string>? ExtendedMetadata { get; }
    public string? ExtendedData { get; }
}
