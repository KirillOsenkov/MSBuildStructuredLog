namespace StructuredLogViewer
{
    public interface ISourceFileResolver
    {
        SourceText GetSourceFileText(string filePath);
    }
}
