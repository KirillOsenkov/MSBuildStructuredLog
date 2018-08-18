namespace StructuredLogViewer.Core.SourceFiles
{
    public interface ISourceFileResolver
    {
        SourceText GetSourceFileText(string filePath);
    }
}
