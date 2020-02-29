using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;

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

        private Folder evaluationFolder;

        public Folder EvaluationFolder
        {
            get
            {
                if (evaluationFolder == null)
                {
                    evaluationFolder = Build.GetOrCreateNodeWithName<Folder>(Intern("Evaluation"));
                }
                return evaluationFolder;
            }
        }

        public Construction()
        {
            Build = new Build();
            Build.Name = "Build";
            this.stringTable = Build.StringTable;
            this.messageProcessor = new MessageProcessor(this, stringTable);
        }

        private string Intern(string text) => stringTable.Intern(text);

        public void BuildStarted(object sender, BuildStartedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    Build.StartTime = args.Timestamp;
                    var properties = Build.GetOrCreateNodeWithName<Folder>(Intern("Environment"));
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

                    if (messageProcessor.DetailedSummary.Length > 0)
                    {
                        var summary = Build.GetOrCreateNodeWithName<Message>(stringTable.Intern(Strings.DetailedSummary));
                        if (messageProcessor.DetailedSummary[0] == '\n')
                        {
                            messageProcessor.DetailedSummary.Remove(0, 1);
                        }

                        summary.Text = messageProcessor.DetailedSummary.ToString();
                    }

                    Build.VisitAllChildren<Project>(p => CalculateTargetGraph(p));
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
                    project.TryAddTarget(orphan);
                }

                return;
            }

            var targetGraph = new TargetGraph(projectInstance);

            IEnumerable<Target> unparentedTargets = null;
            while ((unparentedTargets = project.GetUnparentedTargets()).Any())
            {
                foreach (var unparentedTarget in unparentedTargets)
                {
                    var parents = targetGraph.GetDependents(unparentedTarget.Name);
                    if (parents != null && parents.Any())
                    {
                        foreach (var parent in parents)
                        {
                            var parentNode = project.GetOrAddTargetByName(parent);
                            if (parentNode != null && (parentNode.Id != -1 || parentNode.HasChildren))
                            {
                                parentNode.TryAddTarget(unparentedTarget);
                                break;
                            }
                        }
                    }

                    project.TryAddTarget(unparentedTarget);
                }
            }

            project.VisitAllChildren<Target>(t =>
            {
                if (t.Project == project)
                {
                    var dependencies = targetGraph.GetDependencies(t.Name);
                    if (dependencies != null && dependencies.Any())
                    {
                        t.DependsOnTargets = Intern(string.Join(",", dependencies));
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
                    Project parentProject = null;
                    TreeNode parentNode = null;

                    var parentContext = args?.ParentProjectBuildEventContext;
                    if (parentContext != null)
                    {
                        int parentProjectId = parentContext.ProjectContextId;
                        if (parentProjectId > 0)
                        {
                            parentProject = GetOrAddProject(parentProjectId);
                        }

                        int parentTaskId = parentContext.TaskId;
                        if (parentProject != null && parentTaskId > 0)
                        {
                            parentNode = parentProject.FindFirstDescendant<Task>(t => t.Id == parentTaskId);
                        }
                    }

                    var project = GetOrAddProject(args, parentProject);

                    // only parent the project if it's not already in the tree
                    if (project.Parent == null)
                    {
                        parentNode = parentNode ?? parentProject;

                        if (parentNode != null)
                        {
                            parentNode.AddChild(project);
                        }
                        else
                        {
                            // This is a "Root" project (no parent project).
                            Build.AddChild(project);
                        }
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
            TargetBuiltReason targetBuiltReason = TargetBuiltReason.None;
            if (args is TargetStartedEventArgs targetStartedArgs)
            {
                targetBuiltReason = targetStartedArgs.BuildReason;
            }

            AddTargetCore(
                args,
                Intern(args.TargetName),
                Intern(args.ParentTarget),
                Intern(args.TargetFile),
                targetBuiltReason);
        }

        public static bool ParentAllTargetsUnderProject { get; set; }

        private void AddTargetCore(
            BuildEventArgs args,
            string targetName,
            string parentTargetName,
            string targetFile,
            TargetBuiltReason targetBuiltReason)
        {
            try
            {
                lock (syncLock)
                {
                    var project = GetOrAddProject(args.BuildEventContext.ProjectContextId);
                    var target = project.CreateTarget(targetName, args.BuildEventContext.TargetId);
                    target.NodeId = args.BuildEventContext.NodeId;
                    target.StartTime = args.Timestamp;
                    target.ParentTarget = parentTargetName;
                    target.TargetBuiltReason = targetBuiltReason;

                    if (!ParentAllTargetsUnderProject && !string.IsNullOrEmpty(parentTargetName))
                    {
                        var parentTarget = project.GetOrAddTargetByName(parentTargetName);
                        parentTarget.TryAddTarget(target);
                        //project.TryAddTarget(parentTarget);
                    }
                    else
                    {
                        project.TryAddTarget(target);
                    }

                    target.SourceFilePath = targetFile;
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
                        var targetOutputsFolder = target.GetOrCreateNodeWithName<Folder>(Intern("TargetOutputs"));

                        foreach (ITaskItem targetOutput in args.TargetOutputs)
                        {
                            var item = new Item();
                            item.Text = Intern(targetOutput.ItemSpec);
                            foreach (DictionaryEntry metadata in targetOutput.CloneCustomMetadata())
                            {
                                var metadataNode = new Metadata();
                                metadataNode.Name = Intern(Convert.ToString(metadata.Key));
                                metadataNode.Value = Intern(Convert.ToString(metadata.Value));
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

        private void TargetSkipped(TargetSkippedEventArgs args)
        {
            AddTargetCore(
                args,
                Intern(args.TargetName),
                Intern(args.ParentTarget),
                Intern(args.TargetFile),
                args.BuildReason);
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
                    if (args.GetType().Name == "TargetSkippedEventArgs")
                    {
                        if (args is TargetSkippedEventArgs targetSkipped)
                        {
                            TargetSkipped(targetSkipped);
                        }
                        else
                        {
                            targetSkipped = new TargetSkippedEventArgs();
                            targetSkipped.BuildEventContext = args.BuildEventContext;
                            targetSkipped.TargetName = Intern(Reflector.GetTargetNameFromTargetSkipped(args));
                            targetSkipped.TargetFile = Intern(Reflector.GetTargetFileFromTargetSkipped(args));
                            targetSkipped.ParentTarget = Intern(Reflector.GetParentTargetFromTargetSkipped(args));
                            targetSkipped.BuildReason = Reflector.GetBuildReasonFromTargetSkipped(args);
                            TargetSkipped(targetSkipped);
                        }

                        return;
                    }

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
                        Intern(args.Message),
                        Intern(args.HelpKeyword),
                        Intern(args.SenderName),
                        MessageImportance.Low));
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        public void StatusEventRaised(object sender, BuildStatusEventArgs e)
        {
            try
            {
                lock (syncLock)
                {
                    // This happens when we consume args created by us (deserialized)
                    if (e is ProjectEvaluationStartedEventArgs projectEvaluationStarted)
                    {
                        var evaluationId = Reflector.GetEvaluationId(projectEvaluationStarted.BuildEventContext);
                        var projectName = Intern(projectEvaluationStarted.ProjectFile);
                        var nodeName = Intern(GetEvaluationProjectName(evaluationId, projectName));
                        var projectEvaluation = EvaluationFolder.GetOrCreateNodeWithName<ProjectEvaluation>(nodeName);
                        if (projectEvaluation.ProjectFile == null)
                        {
                            projectEvaluation.ProjectFile = projectName;
                        }

                        // we stash the evaluation Id as a negative ProjectContextId
                        projectEvaluation.Id = evaluationId;
                    }
                    else if (e is ProjectEvaluationFinishedEventArgs projectEvaluationFinished)
                    {
                        var projectName = Intern(projectEvaluationFinished.ProjectFile);
                        var profilerResult = projectEvaluationFinished.ProfilerResult;
                        if (profilerResult != null && projectName != null)
                        {
                            var evaluationId = Reflector.GetEvaluationId(projectEvaluationFinished.BuildEventContext);
                            var nodeName = Intern(GetEvaluationProjectName(evaluationId, projectName));
                            var projectEvaluation = EvaluationFolder.GetOrCreateNodeWithName<ProjectEvaluation>(nodeName);
                            ConstructProfilerResult(projectEvaluation, profilerResult.Value);
                        }
                    }
                    // this happens during live build using MSBuild 15.3 or newer
                    else if (e.GetType().Name == "ProjectEvaluationStartedEventArgs")
                    {
                        var projectName = Intern(TextUtilities.ParseQuotedSubstring(e.Message));
                        var evaluationId = Reflector.GetEvaluationId(e.BuildEventContext);
                        var nodeName = Intern(GetEvaluationProjectName(evaluationId, projectName));
                        var projectEvaluation = EvaluationFolder.GetOrCreateNodeWithName<ProjectEvaluation>(nodeName);
                        projectEvaluation.Id = Reflector.GetEvaluationId(e.BuildEventContext);
                        if (projectEvaluation.ProjectFile == null)
                        {
                            projectEvaluation.ProjectFile = projectName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private static string GetEvaluationProjectName(int evaluationId, string projectName)
        {
            return projectName + " id:" + evaluationId;
        }

        private void ConstructProfilerResult(ProjectEvaluation projectEvaluation, ProfilerResult profilerResult)
        {
            var nodes = new Dictionary<long, EvaluationProfileEntry>();

            foreach (var kvp in profilerResult.ProfiledLocations)
            {
                var location = kvp.Key;
                var result = kvp.Value;

                if (!nodes.TryGetValue(location.Id, out var node))
                {
                    node = new EvaluationProfileEntry();
                    nodes[location.Id] = node;

                    node.ElementName = location.ElementName;
                    node.ElementDescription = location.ElementDescription;
                    node.EvaluationPassDescription = location.EvaluationPassDescription;
                    node.Kind = location.Kind;
                    node.SourceFilePath = location.File;
                    node.LineNumber = location.Line ?? 0;

                    node.AddEntry(result);
                }
            }

            foreach (var kvp in profilerResult.ProfiledLocations)
            {
                var location = kvp.Key;

                if (nodes.TryGetValue(location.Id, out var node))
                {
                    if (location.ParentId.HasValue && nodes.TryGetValue(location.ParentId.Value, out var parentNode))
                    {
                        parentNode.AddChild(node);

                        var parentDuration = parentNode.ProfiledLocation.InclusiveTime.TotalMilliseconds;
                        var duration = node.ProfiledLocation.InclusiveTime.TotalMilliseconds;

                        double ratio = GetRatio(parentDuration, duration);

                        node.Value = ratio;
                    }
                    else
                    {
                        projectEvaluation.AddChildAtBeginning(node);
                        node.Value = 100;
                    }
                }
            }
        }

        private static double GetRatio(double parentDuration, double duration)
        {
            double ratio = 100;
            if (parentDuration != 0)
            {
                ratio = 100 * duration / parentDuration;
                if (ratio < 0)
                {
                    ratio = 0;
                }
                else if (ratio > 100.0)
                {
                    ratio = 100.0;
                }
            }

            return ratio;
        }

        public void WarningRaised(object sender, BuildWarningEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    TreeNode parent = FindParent(args.BuildEventContext);
                    if (parent == null)
                    {
                        parent = Build;
                    }

                    var warnings = parent.GetOrCreateNodeWithName<Folder>(Intern("Warnings"));
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

        private TreeNode FindParent(BuildEventContext buildEventContext)
        {
            Project project = GetOrAddProject(buildEventContext.ProjectContextId);
            TreeNode result = project;
            if (buildEventContext.TargetId > 0)
            {
                var target = project.GetTargetById(buildEventContext.TargetId);
                if (target != null)
                {
                    result = target;
                    if (buildEventContext.TaskId > 0)
                    {
                        var task = target.GetTaskById(buildEventContext.TaskId);
                        if (task != null)
                        {
                            result = task;
                        }
                    }
                }
            }

            return result;
        }

        public void ErrorRaised(object sender, BuildErrorEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    TreeNode parent = FindParent(args.BuildEventContext);
                    if (parent == null)
                    {
                        parent = Build;
                    }

                    var errors = parent.GetOrCreateNodeWithName<Folder>(Intern("Errors"));
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
            message.Text = Intern(args.Message);
            message.Timestamp = args.Timestamp;
            message.Code = Intern(args.Code);
            message.ColumnNumber = args.ColumnNumber;
            message.EndColumnNumber = args.EndColumnNumber;
            message.EndLineNumber = args.EndLineNumber;
            message.LineNumber = args.LineNumber;
            message.File = Intern(args.File);
            message.ProjectFile = Intern(args.ProjectFile);
            message.Subcategory = Intern(args.Subcategory);
        }

        private void Populate(AbstractDiagnostic message, BuildErrorEventArgs args)
        {
            message.Text = Intern(args.Message);
            message.Timestamp = args.Timestamp;
            message.Code = Intern(args.Code);
            message.ColumnNumber = args.ColumnNumber;
            message.EndColumnNumber = args.EndColumnNumber;
            message.EndLineNumber = args.EndLineNumber;
            message.LineNumber = args.LineNumber;
            message.File = Intern(args.File);
            message.ProjectFile = Intern(args.ProjectFile);
            message.Subcategory = Intern(args.Subcategory);
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
        public Project GetOrAddProject(ProjectStartedEventArgs args, Project parentProject = null)
        {
            var projectId = args.BuildEventContext.ProjectContextId;
            Project result = _projectIdToProjectMap.GetOrAdd(projectId,
                id => CreateProject(id));
            result.NodeId = args.BuildEventContext.NodeId;

            UpdateProject(result, args);

            return result;
        }

        public Project GetOrAddProject(int projectId)
        {
            Project result = _projectIdToProjectMap.GetOrAdd(projectId, id => CreateProject(id));
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
                project.Name = Intern(args.Message);
                project.ProjectFile = Intern(args.ProjectFile);
                project.EntryTargets = string.IsNullOrWhiteSpace(args.TargetNames)
                    ? ImmutableArray<string>.Empty
                    : stringTable.InternList(TextUtilities.SplitSemicolonDelimitedList(args.TargetNames));

                var internedGlobalProperties = stringTable.InternStringDictionary(args.GlobalProperties) ?? ImmutableDictionary<string, string>.Empty;

                project.GlobalProperties = internedGlobalProperties;

                if (args.GlobalProperties != null)
                {
                    AddGlobalProperties(project, internedGlobalProperties);
                }

                if (args.Properties != null)
                {
                    var properties = project.GetOrCreateNodeWithName<Folder>(Intern("Properties"));
                    AddProperties(properties, args
                        .Properties
                        .Cast<DictionaryEntry>()
                        .OrderBy(d => d.Key)
                        .Select(d => new KeyValuePair<string, string>(
                            Intern(Convert.ToString(d.Key)),
                            Intern(Convert.ToString(d.Value)))));
                }

                if (args.Items != null)
                {
                    RetrieveProjectInstance(project, args);

                    var items = project.GetOrCreateNodeWithName<Folder>("Items");
                    foreach (DictionaryEntry kvp in args.Items)
                    {
                        var itemName = Intern(Convert.ToString(kvp.Key));
                        var itemGroup = items.GetOrCreateNodeWithName<Folder>(itemName);

                        var item = new Item();

                        var taskItem = kvp.Value as ITaskItem;
                        if (taskItem != null)
                        {
                            item.Text = Intern(taskItem.ItemSpec);
                            foreach (DictionaryEntry metadataName in taskItem.CloneCustomMetadata())
                            {
                                item.AddChild(new Metadata
                                {
                                    Name = Intern(Convert.ToString(metadataName.Key)),
                                    Value = Intern(Convert.ToString(metadataName.Value))
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
            var taskName = Intern(taskStartedEventArgs.TaskName);
            var assembly = Intern(GetTaskAssembly(taskName));
            var taskId = taskStartedEventArgs.BuildEventContext.TaskId;
            var startTime = taskStartedEventArgs.Timestamp;

            Task result;
            switch (taskName)
            {
                case "Copy":
                    result = new CopyTask();
                    break;
                case "Csc":
                    result = new CscTask();
                    break;
                case "Vbc":
                    result = new VbcTask();
                    break;
                case "Fsc":
                    result = new FscTask();
                    break;
                default:
                    result = new Task();
                    break;
            }

            result.Name = taskName;
            result.Id = taskId;
            result.NodeId = taskStartedEventArgs.BuildEventContext.NodeId;
            result.StartTime = startTime;
            result.FromAssembly = assembly;
            result.SourceFilePath = Intern(taskStartedEventArgs.TaskFile);

            return result;
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
            if (properties == null)
            {
                return;
            }

            foreach (var kvp in properties)
            {
                var property = new Property
                {
                    Name = Intern(kvp.Key),
                    Value = Intern(kvp.Value)
                };
                parent.AddChild(property);
            }
        }
    }
}
