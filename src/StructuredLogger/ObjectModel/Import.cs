namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Import : TextNode, IHasRelevance, IPreprocessable, IHasSourceFile, IHasLineNumber
    {
        public string ProjectFilePath { get; set; }
        public string ImportedProjectFilePath { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public Import()
        {
        }

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
        }

        public string Location => $" at ({Line};{Column})";

        private bool isLowRelevance = false;
        public bool IsLowRelevance
        {
            get => isLowRelevance && !IsSelected;
            set => SetField(ref isLowRelevance, value);
        }

        string IPreprocessable.RootFilePath => ImportedProjectFilePath;

        string IHasSourceFile.SourceFilePath => ProjectFilePath;
        int? IHasLineNumber.LineNumber => Line;

        public override string ToString() => $"Import: {Name}{Location}";
    }
}
