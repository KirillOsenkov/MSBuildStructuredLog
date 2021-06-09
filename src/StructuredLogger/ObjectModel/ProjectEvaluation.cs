using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ProjectEvaluation : TimedNode, IPreprocessable, IHasSourceFile, IHasRelevance, IProjectOrEvaluation
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

        public string TargetFramework { get; set; }

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

        private TimedNode importsFolder;
        public TimedNode ImportsFolder
        {
            get
            {
                if (importsFolder == null)
                {
                    importsFolder = new TimedNode
                    {
                        Name = Strings.Imports
                    };
                    this.AddChildAtBeginning(importsFolder);
                }

                return importsFolder;
            }
        }

        private TimedNode propertyReassignmentFolder;
        public TimedNode PropertyReassignmentFolder
        {
            get
            {
                if (propertyReassignmentFolder == null)
                {
                    propertyReassignmentFolder = new TimedNode
                    {
                        Name = Strings.PropertyReassignmentFolder
                    };
                    this.AddChildAtBeginning(propertyReassignmentFolder);
                }

                return propertyReassignmentFolder;
            }
        }

        private Dictionary<string, NamedNode> importsMap = new Dictionary<string, NamedNode>();

        public void AddImport(TextNode textNode)
        {
            NamedNode parent = ImportsFolder;

            IHasSourceFile importOrNot = (IHasSourceFile)textNode;

            if (importOrNot.SourceFilePath != this.ProjectFile && importsMap.TryGetValue(importOrNot.SourceFilePath, out var foundParent))
            {
                parent = foundParent;
            }

            if (importOrNot is Import import && import.ImportedProjectFilePath != null)
            {
                importsMap[import.ImportedProjectFilePath] = import;
            }

            parent.AddChild(textNode);
        }
    }
}
