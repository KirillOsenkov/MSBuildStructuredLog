using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Project : TimedNode, IPreprocessable, IHasSourceFile, IHasRelevance
    {
        /// <summary>
        /// The full path to the MSBuild project file for this project.
        /// </summary>
        public string ProjectFile { get; set; }

        public string ProjectFileExtension => ProjectFile != null 
            ? Path.GetExtension(ProjectFile).ToLowerInvariant() 
            : "";

        public string ProjectDirectory => !string.IsNullOrEmpty(ProjectFile)
            ? Path.GetDirectoryName(ProjectFile)
            : null;

        public string SourceFilePath => ProjectFile;
        string IPreprocessable.RootFilePath => ProjectFile;

        /// <summary>
        /// A lookup table mapping of target names to targets. 
        /// Target names are unique to a project and the id is not always specified in the log.
        /// </summary>
        private readonly ConcurrentDictionary<string, Target> _targetNameToTargetMap = new ConcurrentDictionary<string, Target>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Target> targetsById = new Dictionary<int, Target>();

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append($"Project Name={Name} File={ProjectFile}");
            sb.Append($" Targets=[{string.Join(",", EntryTargets)}]");
            sb.Append($" GlobalProperties=[{string.Join(",", GlobalProperties.Select(kvp => $"{kvp.Key}={kvp.Value}"))}]");

            return sb.ToString();
        }

        public void TryAddTarget(Target target)
        {
            if (target.Parent == null)
            {
                AddChild(target);
            }
        }

        public IEnumerable<Target> GetUnparentedTargets()
        {
            return _targetNameToTargetMap.Values
                .Union(targetsById.Values)
                .Where(t => t.Parent == null)
                .OrderBy(t => t.StartTime)
                .ToArray();
        }

        /// <summary>
        /// Gets the child target by identifier.
        /// </summary>
        /// <remarks>Throws if the child target does not exist</remarks>
        /// <param name="id">The target identifier.</param>
        /// <returns>Target with the given ID</returns>
        public Target GetTargetById(int id)
        {
            if (id == -1)
            {
                throw new ArgumentException("Invalid target id: -1");
            }

            if (targetsById.TryGetValue(id, out var target))
            {
                return target;
            }

            target = _targetNameToTargetMap.Values.FirstOrDefault(t => t.Id == id);
            if (target == null)
            {
                target = CreateTargetInstance(null);
            }

            AssociateTargetWithId(id, target);
            return target;
        }

        private void AssociateTargetWithId(int id, Target target)
        {
            if (id != -1)
            {
                targetsById[id] = target;
            }
        }

        public Target GetOrAddTargetByName(string targetName)
        {
            Target result = _targetNameToTargetMap.GetOrAdd(targetName, key => CreateTargetInstance(key));
            return result;
        }

        public Target CreateTarget(string name, int id)
        {
            if (_targetNameToTargetMap.TryGetValue(name, out var target))
            {
                // they require a specific id
                if (id != -1)
                {
                    // existing target doesn't yet have a specific id,
                    // let's specify it
                    if (target.Id == -1)
                    {
                        target.Id = id;
                        AssociateTargetWithId(id, target);
                    }
                    // existing target has a different id, it's the case where
                    // there are multiple targets with the same name.
                    // We need a completely new target here.
                    else
                    {
                        if (!targetsById.TryGetValue(id, out target))
                        {
                            target = CreateTargetInstance(name);
                            target.Id = id;
                            AssociateTargetWithId(id, target);
                        }
                        else
                        {
                            target.Name = name;
                        }
                    }
                }
            }
            else
            {
                if (id != -1 && targetsById.TryGetValue(id, out target))
                {
                    target.Name = name;
                }
                else
                {
                    target = CreateTargetInstance(name);
                    target.Id = id;
                    AssociateTargetWithId(id, target);
                }

                _targetNameToTargetMap.TryAdd(name, target);
            }

            return target;
        }

        private static Target CreateTargetInstance(string name)
        {
            return new Target()
            {
                Name = name,
                Id = -1
            };
        }

        public Target GetTarget(string targetName, int targetId)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                return GetTargetById(targetId);
            }

            Target result;
            _targetNameToTargetMap.TryGetValue(targetName, out result);
            return result;
        }

        public bool IsLowRelevance
        {
            get => HasFlag(NodeFlags.LowRelevance) && !IsSelected;
            set => SetFlag(NodeFlags.LowRelevance, value);
        }

        public IReadOnlyList<string> EntryTargets { get; set; }
        public IDictionary<string, string> GlobalProperties { get; set; }
    }
}
