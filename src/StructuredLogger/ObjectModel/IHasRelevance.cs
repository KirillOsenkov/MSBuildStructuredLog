namespace Microsoft.Build.Logging.StructuredLogger
{
    public interface IHasRelevance
    {
        bool IsLowRelevance { get; }
    }
}
