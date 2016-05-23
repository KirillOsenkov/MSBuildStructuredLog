using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Execution;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TargetGraph
    {
        private readonly ProjectInstance projectInstance;
        private readonly Dictionary<string, HashSet<string>> dependents = new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, HashSet<string>> dependencies = new Dictionary<string, HashSet<string>>();

        public TargetGraph(ProjectInstance projectInstance)
        {
            this.projectInstance = projectInstance;
            Calculate();
        }

        private void Calculate()
        {
            foreach (var target in projectInstance.Targets)
            {
                dependents[target.Key] = new HashSet<string>();
                dependencies[target.Key] = new HashSet<string>();
            }

            foreach (var target in projectInstance.Targets)
            {
                var targetDependencies = GetTargetDependencies(target.Key);
                dependencies[target.Key].UnionWith(targetDependencies);
                foreach (var dependency in targetDependencies)
                {
                    if (!dependents.ContainsKey(dependency))
                    {
                        dependents.Add(dependency, new HashSet<string>());
                    }

                    dependents[dependency].Add(target.Key);
                }
            }
        }

        public string GetDependent(string target)
        {
            HashSet<string> bucket;
            if (dependents.TryGetValue(target, out bucket))
            {
                return bucket.FirstOrDefault();
            }

            return null;
        }

        public IEnumerable<string> GetDependencies(string target)
        {
            HashSet<string> bucket;
            dependencies.TryGetValue(target, out bucket);
            return bucket ?? Enumerable.Empty<string>();
        }

        public IEnumerable<string> GetTargetClosure(string targetName)
        {
            HashSet<string> visitedTargets = new HashSet<string>();
            AddDependencies(targetName, visitedTargets);
            return visitedTargets;
        }

        private void AddDependencies(string targetName, HashSet<string> visitedTargets)
        {
            if (visitedTargets.Add(targetName))
            {
                foreach (var dependency in GetTargetDependencies(targetName))
                {
                    AddDependencies(dependency, visitedTargets);
                }
            }
        }

        private static readonly char[] targetsSplitChars = new char[] { ';', '\r', '\n', '\t', ' ' };

        private static IEnumerable<string> SplitTargets(string targets)
        {
            return targets.Split(targetsSplitChars, StringSplitOptions.RemoveEmptyEntries);
        }

        private IEnumerable<string> GetTargetDependencies(string targetName)
        {
            ProjectTargetInstance targetInstance;
            if (projectInstance.Targets.TryGetValue(targetName, out targetInstance))
            {
                return SplitTargets(projectInstance.ExpandString(targetInstance.DependsOnTargets));
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }
    }
}
