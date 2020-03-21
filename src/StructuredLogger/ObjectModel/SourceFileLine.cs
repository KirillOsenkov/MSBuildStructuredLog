namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SourceFileLine : TreeNode
    {
        public int LineNumber { get; set; }
        public string LineText { get; set; }

        public override string TypeName => nameof(SourceFileLine);

        public override string ToString()
        {
            return LineNumber.ToString().PadRight(5, ' ') + LineText;
        }
    }
}
