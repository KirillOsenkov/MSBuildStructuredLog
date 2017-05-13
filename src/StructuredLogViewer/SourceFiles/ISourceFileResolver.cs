namespace StructuredLogViewer
{
    public interface ISourceFileResolver
    {
        string GetSourceFileText(string filePath);
    }
}
