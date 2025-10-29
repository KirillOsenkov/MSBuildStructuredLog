using System;

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

            Text = importedProjectFilePath;
        }

        public string Location => $" at ({Line};{Column})";

        public static bool IsImportBefore(string path)
        {
            return path.EndsWith("Sdk.props", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".ImportBefore.targets", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsImportAfter(string path)
        {
            return path.EndsWith("Sdk.targets", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".ImportAfter.targets", StringComparison.OrdinalIgnoreCase);
        }

        public override string TypeName => nameof(Import);

        public bool IsLowRelevance
        {
            get => HasFlag(NodeFlags.LowRelevance) && !IsSelected;
            set => SetFlag(NodeFlags.LowRelevance, value);
        }

        string IPreprocessable.RootFilePath => ImportedProjectFilePath;

        string IHasSourceFile.SourceFilePath => ProjectFilePath;
        int? IHasLineNumber.LineNumber => Line;

        public override string ToString() => $"Import: {Text}{Location}";
    }
}
