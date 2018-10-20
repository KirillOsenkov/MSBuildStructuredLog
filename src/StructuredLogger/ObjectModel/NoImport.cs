namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NoImport : TextNode, IHasRelevance, IHasSourceFile, IHasLineNumber
    {
        public string ProjectFilePath { get; private set; }
        public string ImportedFileSpec { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        public NoImport(
            string projectFilePath,
            string importedFileSpec,
            int line,
            int column,
            string reason)
        {
            ProjectFilePath = projectFilePath;
            Name = importedFileSpec;
            Location = $" at ({line};{column})";
            Line = line;
            Column = column;
            Text = reason;
        }

        public string Location { get; set; }

        private bool isLowRelevance = true;
        public bool IsLowRelevance
        {
            get => isLowRelevance && !IsSelected;
            set => SetField(ref isLowRelevance, value);
        }

        string IHasSourceFile.SourceFilePath => ProjectFilePath;
        int? IHasLineNumber.LineNumber => Line;
    }
}
