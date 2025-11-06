namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SourceFileLine : TreeNode, IHasLineNumber, IHasSourceFile, IHasRelevance
    {
        public string SourceFilePath { get; set; }
        public int LineNumber { get; set; }
        public string LineText { get; set; }

        public override string TypeName => nameof(SourceFileLine);

        int? IHasLineNumber.LineNumber => LineNumber;

        public bool IsLowRelevance
        {
            get => HasFlag(NodeFlags.LowRelevance) && !IsSelected;
            set => SetFlag(NodeFlags.LowRelevance, value);
        }

        public override string ToString()
        {
            return LineNumber.ToString().PadRight(5, ' ') + LineText;
        }
    }
}
