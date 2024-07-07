using System;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public struct ProjectImport : IEquatable<ProjectImport>
    {
        public ProjectImport(string importedProject, int line, int column, Import import)
        {
            ProjectPath = importedProject;
            Line = line;
            Column = column;
            Import = import;
        }

        public string ProjectPath { get; set; }

        /// <summary>
        /// 0-based
        /// </summary>
        public int Line { get; set; }
        public int Column { get; set; }

        public Import Import { get; set; }

        public static bool operator ==(ProjectImport left, ProjectImport right) => left.Equals(right);
        public static bool operator !=(ProjectImport left, ProjectImport right) => !(left == right);

        public bool Equals(ProjectImport other)
        {
            return ProjectPath == other.ProjectPath
                && Line == other.Line
                && Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            if (obj is ProjectImport other)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return (ProjectPath, Line, Column).GetHashCode();
        }

        public override string ToString()
        {
            return $"{ProjectPath} ({Line},{Column})";
        }
    }
}
