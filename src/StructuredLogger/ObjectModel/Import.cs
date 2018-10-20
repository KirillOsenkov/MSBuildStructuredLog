namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Import : TextNode, IHasRelevance, IHasSourceFile, IHasLineNumber
    {
        public string ProjectFilePath { get; private set; }
        public string ImportedProjectFilePath { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        public Import(
            string projectFilePath,
            string importedProjectFilePath,
            int line,
            int column)
        {
            ProjectFilePath = projectFilePath;
            ImportedProjectFilePath = importedProjectFilePath;
            Line = line;
            Column = column;

            Name = importedProjectFilePath;
            Location = $" at ({line};{column})";
        }

        public string Location { get; set; }

        private bool isLowRelevance = false;
        public bool IsLowRelevance
        {
            get => isLowRelevance && !IsSelected;
            set => SetField(ref isLowRelevance, value);
        }

        string IHasSourceFile.SourceFilePath => ProjectFilePath;
        int? IHasLineNumber.LineNumber => Line;
    }
}
