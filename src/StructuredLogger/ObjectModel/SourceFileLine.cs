namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SourceFileLine : TreeNode, IHasLineNumber, IHasSourceFile
    {
        public string SourceFilePath { get; set; }
        public int LineNumber { get; set; }
        public string LineText { get; set; }

        public override string TypeName => nameof(SourceFileLine);

        int? IHasLineNumber.LineNumber => LineNumber;

        public override string ToString()
        {
            return LineNumber.ToString().PadRight(5, ' ') + LineText;
        }
    }
}
