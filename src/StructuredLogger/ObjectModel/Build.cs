using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an MSBuild overall build execution.
    /// </summary>
    public class Build : LogProcessNode
    {
        public bool Succeeded { get; set; }

        private IEnumerable<Project> projectsSortedTopologically;
        public IEnumerable<Project> ProjectsSortedTopologically => projectsSortedTopologically ?? (projectsSortedTopologically = GetProjectsSortedTopologically());

        public IEnumerable<Project> GetProjectsSortedTopologically()
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<Project>();
            foreach (var project in this.Children.OfType<Project>())
            {
                Visit(project, list, visited);
            }

            return list;
        }

        private void Visit(Project project, List<Project> list, HashSet<string> visited)
        {
            if (visited.Add(project.ProjectFile))
            {
                foreach (var childProject in project.Children.OfType<Project>())
                {
                    Visit(childProject, list, visited);
                }

                list.Add(project);
            }
        }

        public IEnumerable<T> EnumerateAllChildren<T>(Predicate<T> predicate)
        {
            var list = new List<T>();
            AddAllChildren(predicate, list);
            return list;
        }
    }
}
