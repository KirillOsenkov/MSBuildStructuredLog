namespace Microsoft.Build.Logging.StructuredLogger;

public interface ISourceFileResolver
{
    SourceText GetSourceFileText(string filePath);
}
