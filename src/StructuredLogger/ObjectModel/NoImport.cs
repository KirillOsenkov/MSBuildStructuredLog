namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NoImport : TextNode, IHasRelevance, IHasSourceFile, IHasLineNumber
    {
        public string ProjectFilePath { get; set; }
        public string ImportedFileSpec { get; set; }
        public string Reason { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public NoImport()
        {
        }

        public override string TypeName => nameof(NoImport);

        public NoImport(
            string projectFilePath,
            string importedFileSpec,
            int line,
            int column,
            string reason)
            : this()
        {
            ProjectFilePath = projectFilePath;
            Text = importedFileSpec;
            Line = line;
            Column = column;
            Reason = reason;
        }

        public string Location => $" at ({Line};{Column})";

        public bool IsLowRelevance
        {
            get => HasFlag(NodeFlags.LowRelevance) && !IsSelected;
            set => SetFlag(NodeFlags.LowRelevance, value);
        }

        string IHasSourceFile.SourceFilePath => ProjectFilePath;
        int? IHasLineNumber.LineNumber => Line;

        public override string ToString() => $"NoImport: {Text}{Location} {Reason}";
    }
}
