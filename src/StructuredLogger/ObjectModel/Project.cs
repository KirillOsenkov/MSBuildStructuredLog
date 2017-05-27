using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Project : TimedNode, IHasSourceFile, IHasRelevance
    {
        /// <summary>
        /// The full path to the MSBuild project file for this project.
        /// </summary>
        public string ProjectFile { get; set; }

        public string ProjectFileExtension => ProjectFile != null ? Path.GetExtension(ProjectFile).ToLowerInvariant() : "";

        public string SourceFilePath => ProjectFile;

        /// <summary>
        /// A lookup table mapping of target names to targets. 
        /// Target names are unique to a project and the id is not always specified in the log.
        /// </summary>
        private readonly ConcurrentDictionary<string, Target> _targetNameToTargetMap = new ConcurrentDictionary<string, Target>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Target> targetsById = new Dictionary<int, Target>();

        public IEnumerable<Target> GetUnparentedTargets()
        {
            return _targetNameToTargetMap.Values.Where(t => t.Parent == null);
        }

        /// <summary>
        /// Gets the child target by identifier.
        /// </summary>
        /// <remarks>Throws if the child target does not exist</remarks>
        /// <param name="id">The target identifier.</param>
        /// <returns>Target with the given ID</returns>
        public Target GetTargetById(int id)
        {
            if (targetsById.TryGetValue(id, out var target))
            {
                return target;
            }

            target = _targetNameToTargetMap.Values.First(t => t.Id == id);
            targetsById[id] = target;
            return target;
        }

        public override string ToString()
        {
            return $"Project Id={Id} Name={Name} File={ProjectFile}";
        }

        public Target GetOrAddTargetByName(string targetName)
        {
            Target result = _targetNameToTargetMap.GetOrAdd(targetName, key => new Target() { Name = key });
            return result;
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

        private bool isLowRelevance = false;
        public bool IsLowRelevance
        {
            get
            {
                return isLowRelevance && !IsSelected;
            }

            set
            {
                if (isLowRelevance == value)
                {
                    return;
                }

                isLowRelevance = value;
                RaisePropertyChanged();
            }
        }
    }
}
