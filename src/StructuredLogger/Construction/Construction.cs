using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Constructs an Object Model graph from MSBuild event arguments
    /// </summary>
    public class Construction
    {
        public Build Build { get; private set; }

        private readonly ConcurrentDictionary<int, Project> _projectIdToProjectMap = new ConcurrentDictionary<int, Project>();

        private readonly ConcurrentDictionary<string, string> _taskToAssemblyMap =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly object syncLock = new object();

        public event Action Completed;

        public Construction()
        {
            this.messageProcessor = new MessageProcessor(this);
        }

        public void BuildStarted(object sender, BuildStartedEventArgs args)
        {
            try
            {
                Build = new Build();
                Build.StartTime = args.Timestamp;
                var properties = Build.GetOrCreateNodeWithName<Folder>("Properties");
                AddProperties(properties, args.BuildEnvironment);

            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void BuildFinished(object sender, BuildFinishedEventArgs args)
        {
            try
            {
                Build.EndTime = args.Timestamp;
                Build.Succeeded = args.Succeeded;

                Build.VisitAllChildren<Project>(p => p.Freeze());

                Completed?.Invoke();
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void ProjectStarted(object sender, ProjectStartedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    Project parent = null;

                    int parentProjectId = args?.ParentProjectBuildEventContext.ProjectContextId ?? -1;
                    if (parentProjectId > 0)
                    {
                        parent = GetOrAddProject(parentProjectId);
                    }

                    var project = GetOrAddProject(args.BuildEventContext.ProjectContextId, args, parent);

                    if (parent != null)
                    {
                        parent.AddChild(project);
                    }
                    else
                    {
                        // This is a "Root" project (no parent project).
                        Build.AddChild(project);
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void ProjectFinished(object sender, ProjectFinishedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    var project = GetOrAddProject(args.BuildEventContext.ProjectContextId);
                    project.EndTime = args.Timestamp;
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void TargetStarted(object sender, TargetStartedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    var project = GetOrAddProject(args.BuildEventContext.ProjectContextId);
                    var target = project.GetOrAddTargetByName(args.TargetName);
                    if (!string.IsNullOrEmpty(args.ParentTarget))
                    {
                        var parentTarget = project.GetOrAddTargetByName(args.ParentTarget);
                        parentTarget.AddChild(target);
                    }
                    else
                    {
                        project.AddChild(target);
                    }

                    target.Id = args.BuildEventContext.TargetId;
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void TargetFinished(object sender, TargetFinishedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    var project = GetOrAddProject(args.BuildEventContext.ProjectContextId);
                    var target = project.GetTarget(args.TargetName, args.BuildEventContext.TargetId);

                    target.EndTime = args.Timestamp;
                    target.Succeeded = args.Succeeded;

                    if (args.TargetOutputs != null)
                    {
                        foreach (var targetOutput in args.TargetOutputs)
                        {
                            // TODO
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void TaskStarted(object sender, TaskStartedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    var project = GetOrAddProject(args.BuildEventContext.ProjectContextId);
                    var target = project.GetTargetById(args.BuildEventContext.TargetId);

                    var task = CreateTask(args);
                    target.AddChild(task);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void TaskFinished(object sender, TaskFinishedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    var project = GetOrAddProject(args.BuildEventContext.ProjectContextId);
                    var target = project.GetTargetById(args.BuildEventContext.TargetId);
                    var task = target.GetTaskById(args.BuildEventContext.TaskId);

                    task.EndTime = args.Timestamp;
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private MessageProcessor messageProcessor;

        public void MessageRaised(object sender, BuildMessageEventArgs args)
        {
            messageProcessor.Process(args);
        }

        public void WarningRaised(object sender, BuildWarningEventArgs args)
        {
            var warnings = Build.GetOrCreateNodeWithName<Folder>("Warnings");
            var warning = new Warning();
            Populate(warning, args);
            warnings.AddChild(warning);
        }

        public void ErrorRaised(object sender, BuildErrorEventArgs args)
        {
            var errors = Build.GetOrCreateNodeWithName<Folder>("Errors");
            var error = new Error();
            Populate(error, args);
            errors.AddChild(error);
        }

        private void Populate(AbstractDiagnostic message, BuildWarningEventArgs args)
        {
            message.Message = args.Message;
            message.Timestamp = args.Timestamp;
            message.Code = args.Code;
            message.ColumnNumber = args.ColumnNumber;
            message.EndColumnNumber = args.EndColumnNumber;
            message.EndLineNumber = args.EndLineNumber;
            message.File = args.File;
            message.LineNumber = args.LineNumber;
            message.ProjectFile = args.ProjectFile;
            message.Subcategory = args.Subcategory;
        }

        private void Populate(AbstractDiagnostic message, BuildErrorEventArgs args)
        {
            message.Message = args.Message;
            message.Timestamp = args.Timestamp;
            message.Code = args.Code;
            message.ColumnNumber = args.ColumnNumber;
            message.EndColumnNumber = args.EndColumnNumber;
            message.EndLineNumber = args.EndLineNumber;
            message.File = args.File;
            message.LineNumber = args.LineNumber;
            message.ProjectFile = args.ProjectFile;
            message.Subcategory = args.Subcategory;
        }

        private void HandleException(Exception ex)
        {
        }

        public static Project CreateProject(int id)
        {
            var result = new Project();
            result.Id = id;
            return result;
        }

        /// <summary>
        /// Gets a project instance for the given identifier. Will create if it doesn't exist.
        /// </summary>
        /// <remarks>If the ProjectStartedEventArgs is not known at this time (null), a stub project is created.</remarks>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="projectStartedEventArgs">The <see cref="ProjectStartedEventArgs"/> instance containing the event data.</param>
        /// <param name="parentProject">The parent project, if any.</param>
        /// <returns>Project object</returns>
        public Project GetOrAddProject(int projectId, ProjectStartedEventArgs projectStartedEventArgs = null, Project parentProject = null)
        {
            Project result = _projectIdToProjectMap.GetOrAdd(projectId,
                id => CreateProject(id));

            if (projectStartedEventArgs != null)
            {
                UpdateProject(result, projectStartedEventArgs);
            }

            return result;
        }

        /// <summary>
        /// Try to update the project data given a project started event. This is useful if the project
        /// was created (e.g. as a parent) before we saw the started event.
        /// <remarks>Does nothing if the data has already been set or the new data is null.</remarks>
        /// </summary>
        /// <param name="projectStartedEventArgs">The <see cref="ProjectStartedEventArgs"/> instance containing the event data.</param>
        public static void UpdateProject(Project project, ProjectStartedEventArgs projectStartedEventArgs)
        {
            if (project.Name == null && projectStartedEventArgs != null)
            {
                project.StartTime = projectStartedEventArgs.Timestamp;
                project.Name = projectStartedEventArgs.Message;
                project.ProjectFile = projectStartedEventArgs.ProjectFile;

                if (projectStartedEventArgs.GlobalProperties != null)
                {
                    AddGlobalProperties(project, projectStartedEventArgs.GlobalProperties);
                }

                if (projectStartedEventArgs.Properties != null)
                {
                    var properties = project.GetOrCreateNodeWithName<Folder>("Properties");
                    AddProperties(properties, projectStartedEventArgs
                        .Properties
                        .Cast<DictionaryEntry>()
                        .Select(d => new KeyValuePair<string, string>(Convert.ToString(d.Key), Convert.ToString(d.Value))));
                }
            }
        }

        private Task CreateTask(TaskStartedEventArgs taskStartedEventArgs)
        {
            var taskName = taskStartedEventArgs.TaskName;
            var assembly = GetTaskAssembly(taskStartedEventArgs.TaskName);
            var taskId = taskStartedEventArgs.BuildEventContext.TaskId;
            var startTime = taskStartedEventArgs.Timestamp;

            Task result;
            if (taskName == "Copy")
            {
                result = new CopyTask()
                {
                    Name = taskName,
                    Id = taskId,
                    StartTime = startTime,
                    FromAssembly = assembly
                };
                return result;
            }

            var task = new Task
            {
                Name = taskName,
                Id = taskId,
                StartTime = startTime,
                FromAssembly = assembly
            };

            return task;
        }

        /// <summary>
        /// Gets the task assembly.
        /// </summary>
        /// <param name="taskName">Name of the task.</param>
        /// <returns>The assembly location for the task.</returns>
        public string GetTaskAssembly(string taskName)
        {
            string assembly;
            return _taskToAssemblyMap.TryGetValue(taskName, out assembly) ? assembly : string.Empty;
        }

        /// <summary>
        /// Sets the assembly location for a given task.
        /// </summary>
        /// <param name="taskName">Name of the task.</param>
        /// <param name="assembly">The assembly location.</param>
        public void SetTaskAssembly(string taskName, string assembly)
        {
            _taskToAssemblyMap.GetOrAdd(taskName, t => assembly);
        }

        private static void AddGlobalProperties(Project project, IEnumerable<KeyValuePair<string, string>> properties)
        {
            var propertiesNode = project.GetOrCreateNodeWithName<Folder>("Properties");
            if (properties != null && properties.Any())
            {
                var global = propertiesNode.GetOrCreateNodeWithName<Folder>("Global");
                AddProperties(global, properties);
            }
        }

        private static void AddProperties(LogProcessNode parent, IEnumerable<KeyValuePair<string, string>> properties)
        {
            foreach (var kvp in properties)
            {
                var property = new Property(kvp);
                parent.AddChild(property);
            }
        }
    }
}
