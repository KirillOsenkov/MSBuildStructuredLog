using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger;

public interface IHasExtendedData
{
    string ExtendedType { get; }
    IDictionary<string, string?>? ExtendedMetadata { get; }
    string? ExtendedData { get; }
}
