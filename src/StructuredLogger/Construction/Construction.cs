using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Execution;
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

        private readonly ConcurrentDictionary<Project, ProjectInstance> _projectToProjectInstanceMap =
            new ConcurrentDictionary<Project, ProjectInstance>();

        private readonly object syncLock = new object();

        private readonly MessageProcessor messageProcessor;
        private readonly StringCache stringTable;

        public event Action Completed;

        public Construction()
        {
            Build = new Build();
            this.stringTable = Build.StringTable;
            this.messageProcessor = new MessageProcessor(this, stringTable);
        }

        public void BuildStarted(object sender, BuildStartedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    Build.StartTime = args.Timestamp;
                    var properties = Build.GetOrCreateNodeWithName<Folder>("Environment");
                    AddProperties(properties, args.BuildEnvironment);
                }

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
                lock (syncLock)
                {
                    Build.EndTime = args.Timestamp;
                    Build.Succeeded = args.Succeeded;

                    Build.VisitAllChildren<Project>(p => CalculateTargetGraph(p));

                    Completed?.Invoke();
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void CalculateTargetGraph(Project project)
        {
            ProjectInstance projectInstance;
            if (!_projectToProjectInstanceMap.TryGetValue(project, out projectInstance))
            {
                // if for some reason we weren't able to fish out the project instance from MSBuild,
                // just add all orphans directly to the project
                var unparented = project.GetUnparentedTargets();
                foreach (var orphan in unparented)
                {
                    project.AddChild(orphan);
                }

                return;
            }

            var targetGraph = new TargetGraph(projectInstance);

            var unparentedTargets = project.GetUnparentedTargets();
            foreach (var unparentedTarget in unparentedTargets)
            {
                var parent = targetGraph.GetDependent(unparentedTarget.Name);
                if (parent != null)
                {
                    var parentNode = project.GetOrAddTargetByName(parent);
                    if (parentNode != null)
                    {
                        parentNode.AddChild(unparentedTarget);
                    }
                }

                if (unparentedTarget.Parent == null)
                {
                    project.AddChild(unparentedTarget);
                }
            }

            project.VisitAllChildren<Target>(t =>
            {
                if (t.Project == project)
                {
                    var dependencies = targetGraph.GetDependencies(t.Name);
                    if (dependencies != null && dependencies.Any())
                    {
                        t.DependsOnTargets = stringTable.Intern(string.Join(",", dependencies));
                    }
                }
            });
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
                    var target = project.GetOrAddTargetByName(stringTable.Intern(args.TargetName));
                    if (!string.IsNullOrEmpty(args.ParentTarget))
                    {
                        var parentTarget = project.GetOrAddTargetByName(stringTable.Intern(args.ParentTarget));
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
                    var target = project.GetTarget(stringTable.Intern(args.TargetName), args.BuildEventContext.TargetId);

                    target.EndTime = args.Timestamp;
                    target.Succeeded = args.Succeeded;

                    if (args.TargetOutputs != null)
                    {
                        var targetOutputsFolder = target.GetOrCreateNodeWithName<Folder>("TargetOutputs");

                        foreach (ITaskItem targetOutput in args.TargetOutputs)
                        {
                            var item = new Item();
                            item.Text = stringTable.Intern(targetOutput.ItemSpec);
                            foreach (DictionaryEntry metadata in targetOutput.CloneCustomMetadata())
                            {
                                var metadataNode = new Metadata();
                                metadataNode.Name = stringTable.Intern(Convert.ToString(metadata.Key));
                                metadataNode.Value = stringTable.Intern(Convert.ToString(metadata.Value));
                                item.AddChild(metadataNode);
                            }

                            targetOutputsFolder.AddChild(item);
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

        public void MessageRaised(object sender, BuildMessageEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    messageProcessor.Process(args);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void CustomEventRaised(object sender, CustomBuildEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    messageProcessor.Process(new BuildMessageEventArgs(
                            stringTable.Intern(args.Message),
                            stringTable.Intern(args.HelpKeyword),
                            stringTable.Intern(args.SenderName),
                            MessageImportance.Low));
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void WarningRaised(object sender, BuildWarningEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    TreeNode parent = GetOrAddProject(args.BuildEventContext.ProjectContextId);
                    if (parent == null)
                    {
                        parent = Build;
                    }

                    var warnings = parent.GetOrCreateNodeWithName<Folder>("Warnings");
                    var warning = new Warning();
                    Populate(warning, args);
                    warnings.AddChild(warning);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void ErrorRaised(object sender, BuildErrorEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    TreeNode parent = GetOrAddProject(args.BuildEventContext.ProjectContextId);
                    if (parent == null)
                    {
                        parent = Build;
                    }

                    var errors = parent.GetOrCreateNodeWithName<Folder>("Errors");
                    var error = new Error();
                    Populate(error, args);
                    errors.AddChild(error);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void Populate(AbstractDiagnostic message, BuildWarningEventArgs args)
        {
            message.Text = stringTable.Intern(args.Message);
            message.Timestamp = args.Timestamp;
            message.Code = stringTable.Intern(args.Code);
            message.ColumnNumber = args.ColumnNumber;
            message.EndColumnNumber = args.EndColumnNumber;
            message.EndLineNumber = args.EndLineNumber;
            message.LineNumber = args.LineNumber;
            message.File = stringTable.Intern(args.File);
            message.ProjectFile = stringTable.Intern(args.ProjectFile);
            message.Subcategory = stringTable.Intern(args.Subcategory);
        }

        private void Populate(AbstractDiagnostic message, BuildErrorEventArgs args)
        {
            message.Text = stringTable.Intern(args.Message);
            message.Timestamp = args.Timestamp;
            message.Code = stringTable.Intern(args.Code);
            message.ColumnNumber = args.ColumnNumber;
            message.EndColumnNumber = args.EndColumnNumber;
            message.EndLineNumber = args.EndLineNumber;
            message.LineNumber = args.LineNumber;
            message.File = stringTable.Intern(args.File);
            message.ProjectFile = stringTable.Intern(args.ProjectFile);
            message.Subcategory = stringTable.Intern(args.Subcategory);
        }

        private void HandleException(Exception ex)
        {
            ErrorReporting.ReportException(ex);

            try
            {
                lock (syncLock)
                {
                    Build.AddChild(new Error() { Text = ex.ToString() });
                }
            }
            catch (Exception)
            {
            }
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
        /// <param name="args">The <see cref="ProjectStartedEventArgs"/> instance containing the event data.</param>
        /// <param name="parentProject">The parent project, if any.</param>
        /// <returns>Project object</returns>
        public Project GetOrAddProject(int projectId, ProjectStartedEventArgs args = null, Project parentProject = null)
        {
            Project result = _projectIdToProjectMap.GetOrAdd(projectId,
                id => CreateProject(id));

            if (args != null)
            {
                UpdateProject(result, args);
            }

            return result;
        }

        /// <summary>
        /// Try to update the project data given a project started event. This is useful if the project
        /// was created (e.g. as a parent) before we saw the started event.
        /// <remarks>Does nothing if the data has already been set or the new data is null.</remarks>
        /// </summary>
        /// <param name="args">The <see cref="ProjectStartedEventArgs"/> instance containing the event data.</param>
        public void UpdateProject(Project project, ProjectStartedEventArgs args)
        {
            if (project.Name == null && args != null)
            {
                project.StartTime = args.Timestamp;
                project.Name = stringTable.Intern(args.Message);
                project.ProjectFile = stringTable.Intern(args.ProjectFile);

                if (args.GlobalProperties != null)
                {
                    AddGlobalProperties(project, args.GlobalProperties);
                }

                if (args.Properties != null)
                {
                    var properties = project.GetOrCreateNodeWithName<Folder>("Properties");
                    AddProperties(properties, args
                        .Properties
                        .Cast<DictionaryEntry>()
                        .OrderBy(d => d.Key)
                        .Select(d => new KeyValuePair<string, string>(
                            stringTable.Intern(Convert.ToString(d.Key)),
                            stringTable.Intern(Convert.ToString(d.Value)))));
                }

                if (args.Items != null)
                {
                    RetrieveProjectInstance(project, args);

                    var items = project.GetOrCreateNodeWithName<Folder>("Items");
                    foreach (DictionaryEntry kvp in args.Items)
                    {
                        var itemName = stringTable.Intern(Convert.ToString(kvp.Key));
                        var itemGroup = items.GetOrCreateNodeWithName<Folder>(itemName);

                        var item = new Item();

                        var taskItem = kvp.Value as ITaskItem2;
                        if (taskItem != null)
                        {
                            item.Text = stringTable.Intern(taskItem.ItemSpec);
                            foreach (DictionaryEntry metadataName in taskItem.CloneCustomMetadata())
                            {
                                item.AddChild(new Metadata
                                {
                                    Name = stringTable.Intern(Convert.ToString(metadataName.Key)),
                                    Value = stringTable.Intern(Convert.ToString(metadataName.Value))
                                });
                            }

                            itemGroup.AddChild(item);
                        }
                    }
                }
            }
        }

        // normally MSBuild internal data structures aren't available to loggers, but we really want access
        // to get at the target graph.
        private void RetrieveProjectInstance(Project project, ProjectStartedEventArgs args)
        {
            if (_projectToProjectInstanceMap.ContainsKey(project))
            {
                return;
            }

            var projectItemInstanceEnumeratorProxy = args?.Items;
            if (projectItemInstanceEnumeratorProxy == null)
            {
                return;
            }

            var _backingItems = GetField(projectItemInstanceEnumeratorProxy, "_backingItems");
            if (_backingItems == null)
            {
                return;
            }

            var _backingEnumerable = GetField(_backingItems, "_backingEnumerable");
            if (_backingEnumerable == null)
            {
                return;
            }

            var _nodes = GetField(_backingEnumerable, "_nodes") as IDictionary;
            if (_nodes == null || _nodes.Count == 0)
            {
                return;
            }

            var projectItemInstance = _nodes.Keys.OfType<object>().FirstOrDefault() as ProjectItemInstance;
            if (projectItemInstance == null)
            {
                return;
            }

            var projectInstance = projectItemInstance.Project;
            if (projectInstance == null)
            {
                return;
            }

            _projectToProjectInstanceMap[project] = projectInstance;
        }

        private static object GetField(object instance, string fieldName)
        {
            return instance?
                .GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?
                .GetValue(instance);
        }

        private Task CreateTask(TaskStartedEventArgs taskStartedEventArgs)
        {
            var taskName = stringTable.Intern(taskStartedEventArgs.TaskName);
            var assembly = stringTable.Intern(GetTaskAssembly(taskStartedEventArgs.TaskName));
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

        private void AddGlobalProperties(Project project, IEnumerable<KeyValuePair<string, string>> properties)
        {
            var propertiesNode = project.GetOrCreateNodeWithName<Folder>("Properties");
            if (properties != null && properties.Any())
            {
                var global = propertiesNode.GetOrCreateNodeWithName<Folder>("Global");
                AddProperties(global, properties);
            }
        }

        private void AddProperties(TreeNode parent, IEnumerable<KeyValuePair<string, string>> properties)
        {
            foreach (var kvp in properties)
            {
                var property = new Property
                {
                    Name = stringTable.Intern(kvp.Key),
                    Value = stringTable.Intern(kvp.Value)
                };
                parent.AddChild(property);
            }
        }
    }
}
