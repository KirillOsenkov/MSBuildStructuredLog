using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class AbstractDiagnostic : TextNode
    {
        public DateTime Timestamp { get; set; }
        public string Code { get; set; }
        public int ColumnNumber { get; set; }
        public int EndColumnNumber { get; set; }
        public int EndLineNumber { get; set; }
        public string File { get; set; }
        public int LineNumber { get; set; }
        public string ProjectFile { get; set; }
        public string Subcategory { get; set; }
    }
}
