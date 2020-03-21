namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Folder : NamedNode, IHasRelevance
    {
        public bool IsLowRelevance
        {
            get => HasFlag(NodeFlags.LowRelevance) && !IsSelected;
            set => SetFlag(NodeFlags.LowRelevance, value);
        }

        public override string TypeName => nameof(Folder);
    }
}
