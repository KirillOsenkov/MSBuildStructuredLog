namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NoImport : TextNode, IHasRelevance, IHasSourceFile, IHasLineNumber
    {
        public string ProjectFilePath { get; set; }
        public string ImportedFileSpec { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public NoImport()
        {
        }

        public NoImport(
            string projectFilePath,
            string importedFileSpec,
            int line,
            int column,
            string reason)
        {
            ProjectFilePath = projectFilePath;
            Name = importedFileSpec;
            Line = line;
            Column = column;
            Text = reason;
        }

        public string Location => $" at ({Line};{Column})";

        private bool isLowRelevance = true;
        public bool IsLowRelevance
        {
            get => isLowRelevance && !IsSelected;
            set => SetField(ref isLowRelevance, value);
        }

        string IHasSourceFile.SourceFilePath => ProjectFilePath;
        int? IHasLineNumber.LineNumber => Line;

        public override string ToString() => $"NoImport: {Name}{Location} {Text}";
    }
}
