using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an MSBuild project execution.
    /// </summary>
    public class Project : LogProcessNode
    {
        /// <summary>
        /// The full path to the MSBuild project file for this project.
        /// </summary>
        public string ProjectFile { get; set; }

        /// <summary>
        /// A lookup table mapping of target names to targets. 
        /// Target names are unique to a project and the id is not always specified in the log.
        /// </summary>
        private readonly ConcurrentDictionary<string, Target> _targetNameToTargetMap = new ConcurrentDictionary<string, Target>(StringComparer.OrdinalIgnoreCase);

        public void Freeze()
        {
            // We could be in a situation where we never saw a "Parent" Target. So it's now
            // in our scope but not rooted. This can happen when targets fail to run.
            // Let's just add them back.
            foreach (var orphan in _targetNameToTargetMap.Values.Where(t => t.Id < 0))
            {
                AddChild(orphan);
            }
        }

        /// <summary>
        /// Gets the child target by identifier.
        /// </summary>
        /// <remarks>Throws if the child target does not exist</remarks>
        /// <param name="id">The target identifier.</param>
        /// <returns>Target with the given ID</returns>
        public Target GetTargetById(int id)
        {
            return _targetNameToTargetMap.Values.First(t => t.Id == id);
        }

        public override string ToString()
        {
            return $"Project Id={Id} Name={Name}";
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
                return _targetNameToTargetMap.Values.First(t => t.Id == targetId);
            }

            return _targetNameToTargetMap[targetName];
        }
    }
}
