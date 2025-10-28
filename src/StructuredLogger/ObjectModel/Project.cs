using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Project : TimedNode, IPreprocessable, IHasSourceFile, IHasRelevance, IProjectOrEvaluation
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

        private readonly Dictionary<int, Target> targetsById = new Dictionary<int, Target>();
        private readonly Dictionary<int, Task> tasksById = new Dictionary<int, Task>();

        public override string TypeName => nameof(Project);

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append($"Project Name={Name} File={ProjectFile}");
            if (EntryTargets != null && EntryTargets.Any())
            {
                sb.Append($" Targets=[{string.Join(", ", EntryTargets)}]");
            }

            if (GlobalProperties != null)
            {
                sb.Append($" GlobalProperties=[{string.Join(", ", GlobalProperties.Select(kvp => $"{kvp.Key}={TextUtilities.ShortenValue(kvp.Value, "...", maxChars: 150)}"))}]");
            }

            return sb.ToString();
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

            targetsById.TryGetValue(id, out var target);

            return target;
        }

        public Target CreateTarget(string name, int id)
        {
            var target = CreateTargetInstance(name);

            target.Id = id;

            targetsById[id] = target;

            return target;
        }

        private int unparentedTargetIndex = 0;

        private Target CreateTargetInstance(string name)
        {
            unparentedTargetIndex++;

            return new Target()
            {
                Name = name,
                Id = -1,
                Index = unparentedTargetIndex, // additional sorting to preserve the order unparented targets are listed in
            };
        }

        public bool IsLowRelevance
        {
            get => HasFlag(NodeFlags.LowRelevance) && !IsSelected;
            set => SetFlag(NodeFlags.LowRelevance, value);
        }

        public IReadOnlyList<string> EntryTargets { get; set; } = Array.Empty<string>();
        public string TargetsText { get; set; }

        public string TargetsDisplayText
        {
            get
            {
                if (string.IsNullOrEmpty(TargetsText))
                {
                    return string.Empty;
                }

                return $" → {TargetsText}";
            }
        }

        public string AdornmentString => this.GetAdornmentString();

        public string TargetFramework { get; set; }
        public bool IsOuterProject { get; set; }

        public string Platform { get; set; }

        public string Configuration { get; set; }

        public int EvaluationId { get; set; }

        /// <summary>
        /// Used for checking whether TargetSkipped.OriginalBuildEventContext is valid and
        /// points to a valid project from this build.
        /// </summary>
        public int ProjectInstanceId { get; set; }

        public string EvaluationText { get; set; } = "";

        public IDictionary<string, string> GlobalProperties { get; set; } = ImmutableDictionary<string, string>.Empty;

        public override string ToolTip
        {
            get
            {
                var sb = new StringBuilder();

                sb.AppendLine(ProjectFile);

                if (EntryTargets != null && EntryTargets.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("Targets:");
                    foreach (var target in EntryTargets.OrderBy(target => target, StringComparer.InvariantCultureIgnoreCase))
                    {
                        sb.AppendLine(target);
                    }
                }

                if (GlobalProperties != null && GlobalProperties.Any())
                {
                    const int maxPropertiesToPrint = 5;
                    sb.AppendLine();
                    sb.AppendLine("Global Properties:");
                    foreach (var pair in GlobalProperties.OrderBy(pair => pair.Key, StringComparer.InvariantCultureIgnoreCase).Take(maxPropertiesToPrint))
                    {
                        sb.AppendLine($"{pair.Key} = {TextUtilities.ShortenValue(pair.Value, "...", maxChars: 150)}");
                    }

                    if (GlobalProperties.Count > maxPropertiesToPrint)
                    {
                        sb.AppendLine("...");
                    }
                }

                sb.AppendLine();
                sb.Append(GetTimeAndDurationText());

                return sb.ToString();
            }
        }

        public void OnTaskAdded(Task task)
        {
            int id = task.Id;

            // It can happen that a ProjectStarted event arrives before the TaskStarted for the parent
            // MSBuild task. If we're the real MSBuild task, reparent those temporarily orphaned
            // projects under this real (presumably MSBuild) task.
            if (tasksById.TryGetValue(id, out var placeholder))
            {
                if (placeholder.HasChildren)
                {
                    foreach (var child in placeholder.Children)
                    {
                        child.Parent = null;
                        task.AddChild(child);
                    }
                }
            }

            tasksById[id] = task;
        }

        public Task GetTaskById(int id)
        {
            if (!tasksById.TryGetValue(id, out var task))
            {
                // The MSBuild task that caused this project to build hasn't arrived yet.
                // Temporarily parent under a placeholder.
                task = new PlaceholderTask { Id = id };
                tasksById[id] = task;
            }

            return task;
        }

        public Target FindTarget(string targetName)
        {
            return Children.OfType<Target>().Where(t => t.Name == targetName).FirstOrDefault();
        }
    }
}
