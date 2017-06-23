namespace Microsoft.Build.Logging.StructuredLogger
{
    public interface IHasLineNumber
    {
        int? LineNumber { get; }
    }
}
