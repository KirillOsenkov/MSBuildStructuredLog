using System.IO;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ProjectEvaluation : TimedNode, IPreprocessable, IHasSourceFile, IHasRelevance
    {
        /// <summary>
        /// The full path to the MSBuild project file for this project.
        /// </summary>
        public string ProjectFile { get; set; }
        public string SourceFilePath => ProjectFile;
        string IPreprocessable.RootFilePath => ProjectFile;
        public string ProjectFileExtension => ProjectFile != null
            ? Path.GetExtension(ProjectFile).ToLowerInvariant()
            : "";

        public string EvaluationText { get; set; } = "";

        public double RelativeDuration { get; set; }

        public override string TypeName => nameof(ProjectEvaluation);

        public bool IsLowRelevance
        {
            get => HasFlag(NodeFlags.LowRelevance) && !IsSelected;
            set => SetFlag(NodeFlags.LowRelevance, value);
        }

        public override string ToString() => $"Evaluation Project={Name} File={ProjectFile}";

        public override string ToolTip
        {
            get
            {
                return $"{ProjectFile}\n{GetTimeAndDurationText()}";
            }
        }
    }
}
