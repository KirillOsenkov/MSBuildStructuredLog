namespace Microsoft.Build.Logging.StructuredLogger
{
    public class SourceFileLine : TreeNode
    {
        public int LineNumber { get; set; }
        public string LineText { get; set; }
    }
}
