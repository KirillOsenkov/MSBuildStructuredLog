using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
using StructuredLogger.BinaryLogger;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Constructs an Object Model graph from MSBuild event arguments
    /// </summary>
    public class Construction
    {
        public Build Build { get; private set; }

        private readonly ConcurrentDictionary<int, Project> _projectIdToProjectMap = new ConcurrentDictionary<int, Project>();

        private readonly Dictionary<string, string> _taskToAssemblyMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly object syncLock = new object();

        private readonly MessageProcessor messageProcessor;
        private readonly StringCache stringTable;

        public StringCache StringTable => stringTable;

        public NamedNode EvaluationFolder => Build.EvaluationFolder;
        public NamedNode EnvironmentFolder => Build.EnvironmentFolder;

        public bool IsLargeBinlog { get; set; }

        public Construction()
        {
            Build = new Build();
            Build.Name = "Build";
            this.stringTable = Build.StringTable;
            this.messageProcessor = new MessageProcessor(this, stringTable);
            Intern(Strings.Assembly);
            Intern(Strings.CommandLineArguments);
            Intern(Strings.DoubleWrites);
            Intern(Strings.Errors);
            Intern(Strings.Task);
            Intern(Strings.Evaluation);
            Intern(Strings.NoImportEmptyExpression);
            Intern(Strings.NoImportNoMatches);
            Intern(Strings.NoImportMissingFile);
            Intern(Strings.NoImportInvalidFile);
            Intern(Strings.Note);
            Intern(Strings.OutputItems);
            Intern(Strings.Parameters);
            Intern(Strings.Properties);
            Intern(Strings.TargetOutputs);
            Intern(Strings.UnusedLocations);
            Intern(Strings.Warnings);
            Intern(nameof(AddItem));
            Intern(nameof(CriticalBuildMessage));
            Intern(nameof(CopyTask));
            Intern(nameof(RobocopyTask));
            Intern(nameof(CscTask));
            Intern(nameof(VbcTask));
            Intern(nameof(FscTask));
            Intern(nameof(MSBuildTask));
            Intern(nameof(ResolveAssemblyReferenceTask));
            Intern(nameof(EntryTarget));
            Intern(nameof(Error));
            Intern(nameof(Folder));
            Intern(nameof(Import));
            Intern(nameof(Item));
            Intern(nameof(Message));
            Intern(nameof(Metadata));
            Intern(nameof(NoImport));
            Intern(nameof(Package));
            Intern(nameof(Parameter));
            Intern(nameof(Project));
            Intern(nameof(ProjectEvaluation));
            Intern(nameof(Property));
            Intern(nameof(RemoveItem));
            Intern(nameof(Target));
            Intern(nameof(Task));
            Intern(nameof(TimedNode));
            Intern(nameof(TimedMessage));
            Intern(nameof(Warning));
        }

        public void Shutdown()
        {
        }

        private string Intern(string text) => stringTable.Intern(text);

        private string SoftIntern(string text) => stringTable.SoftIntern(text);

        private readonly HashSet<string> environmentVariablesUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void AddEnvironmentVariable(string environmentVariableName, string environmentVariableValue)
        {
            if (environmentVariablesUsed.Add(environmentVariableName))
            {
                var property = new Property { Name = environmentVariableName, Value = environmentVariableValue };
                EnvironmentFolder.AddChild(property);
            }
        }

        public void BuildStarted(object sender, BuildStartedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    Build.StartTime = args.Timestamp;

                    // Since we saw BuildStarted we now need to see BuildFinished,
                    // otherwise the build was cancelled or interrupted
                    Build.Succeeded = false;

                    if (args.BuildEnvironment?.Count > 0)
                    {
                        AddProperties(EnvironmentFolder, args.BuildEnvironment);
                    }

                    // realize the evaluation folder now so it is ordered before the main solution node
                    _ = EvaluationFolder;
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

                    EnvironmentFolder.AddChild(new Note
                    {
                        Text = Intern(Strings.TruncatedEnvironment)
                    });

                    if (messageProcessor.DetailedSummary.Length > 0)
                    {
                        var summary = new Message();
                        Build.AddChild(summary);

                        if (messageProcessor.DetailedSummary[0] == '\n')
                        {
                            messageProcessor.DetailedSummary.Remove(0, 1);
                        }

                        string fullText = messageProcessor.DetailedSummary.ToString();
#if DEBUG
                        fullText = Intern(fullText);
#endif
                        summary.Text = fullText;
                    }

                    //Build.VisitAllChildren<Project>(p => CalculateTargetGraph(p));
                }
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
                            parentNode = parentProject.GetTaskById(parentTaskId);
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
            AddTargetCore(
                args,
                Intern(args.TargetName),
                Intern(args.ParentTarget),
                Intern(args.TargetFile),
                args.BuildReason);
        }

        private Target AddTargetCore(
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
                    target.EndTime = target.StartTime; // will properly set later
                    target.ParentTarget = parentTargetName;
                    target.TargetBuiltReason = targetBuiltReason;
                    target.SourceFilePath = targetFile;

                    project.AddChild(target);

                    return target;
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }

            return null;
        }

        public void TargetFinished(object sender, TargetFinishedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    var project = GetOrAddProject(args.BuildEventContext.ProjectContextId);
                    var target = project.GetTargetById(args.BuildEventContext.TargetId);

                    target.EndTime = args.Timestamp;
                    target.Succeeded = args.Succeeded;

                    if (args.TargetOutputs != null)
                    {
                        var targetOutputsFolder = target.GetOrCreateNodeWithName<Folder>(Strings.TargetOutputs);
                        targetOutputsFolder.DisableChildrenCache = true;

                        foreach (ITaskItem targetOutput in args.TargetOutputs)
                        {
                            var item = new Item();
                            item.Text = SoftIntern(targetOutput.ItemSpec);

                            var metadataArray = targetOutput.CloneCustomMetadata();
                            if (metadataArray.Count > 0)
                            {
                                if (metadataArray is ArrayDictionary<string, string> array)
                                {
                                    foreach (var metadata in array)
                                    {
                                        var metadataNode = new Metadata();
                                        metadataNode.Name = SoftIntern(metadata.Key);
                                        metadataNode.Value = SoftIntern(metadata.Value);
                                        item.AddChild(metadataNode);
                                    }
                                }
                                else
                                {
                                    // This should be unreachable or legacy scenarios only
                                    // (someone passing StructuredLogger directly to MSBuild)
                                    foreach (DictionaryEntry metadata in metadataArray)
                                    {
                                        if (metadata.Key is string key && metadata.Value is string value)
                                        {
                                            var metadataNode = new Metadata();
                                            metadataNode.Name = SoftIntern(key);
                                            metadataNode.Value = SoftIntern(value);
                                            item.AddChild(metadataNode);
                                        }
                                    }
                                }
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

        public void TargetSkipped(TargetSkippedEventArgs args)
        {
            string targetName = Intern(args.TargetName);
            string messageText = args.Message;

            var originalBuildEventContext = args.OriginalBuildEventContext;
            var skipReason = args.SkipReason;
            if ((skipReason == TargetSkipReason.PreviouslyBuiltSuccessfully ||
                skipReason == TargetSkipReason.PreviouslyBuiltUnsuccessfully) && originalBuildEventContext != null)
            {
                var prefix = "Target \"" + targetName + "\" "; // trim the Target Name text since the node will already display that
                if (messageText.StartsWith(prefix, StringComparison.Ordinal))
                {
                    messageText = messageText.Substring(prefix.Length);
                }
            }

            messageText = Intern(messageText);

            Target target = null;

            var project = GetProject(args.BuildEventContext.ProjectContextId);
            if (project != null && args.BuildEventContext.TargetId != BuildEventContext.InvalidTargetId)
            {
                target = project.FindLastChild<Target, TargetSkippedEventArgs>(static (t, args) => t.Id == args.BuildEventContext.TargetId, args);
            }

            if (target == null)
            {
                target = AddTargetCore(
                    args,
                    targetName,
                    Intern(args.ParentTarget),
                    Intern(args.TargetFile),
                    args.BuildReason);
            }

            if (originalBuildEventContext != null && originalBuildEventContext.ProjectContextId != BuildEventContext.InvalidProjectContextId)
            {
                var originalProject = GetProject(originalBuildEventContext.ProjectContextId);
                if (originalProject != null)
                {
                    target.ParentTarget = messageText;
                    if (originalBuildEventContext.TargetId != -1 &&
                        originalProject.GetTargetById(originalBuildEventContext.TargetId) is Target originalTarget)
                    {
                        target.OriginalNode = originalTarget;
                    }
                    else
                    {
                        // the original target was skipped because of false condition, so its target id == -1
                        // Need to look it up by name, if unambiguous
                        var candidates = originalProject
                            .Children
                            .OfType<Target>()
                            .Where(t => t.Name == targetName)
                            .ToArray();
                        if (candidates.Length == 1)
                        {
                            originalTarget = candidates[0];
                        }
                        else
                        {
                            originalTarget = null;
                        }

                        target.OriginalNode = (TimedNode)originalTarget ?? originalProject;
                    }
                }
            }
            else
            {
                var messageNode = new Message { Text = messageText };
                target.AddChild(messageNode);
            }

            target.Skipped = true;
        }

        public void TaskStarted(object sender, TaskStartedEventArgs args)
        {
            try
            {
                lock (syncLock)
                {
                    Build.Statistics.Tasks++;

                    var project = GetOrAddProject(args.BuildEventContext.ProjectContextId);
                    var target = project.GetTargetById(args.BuildEventContext.TargetId);

                    var task = CreateTask(args);
                    target.AddChild(task);
                    project.OnTaskAdded(task);

                    if (args is TaskStartedEventArgs2 taskStarted2)
                    {
                        task.LineNumber = taskStarted2.LineNumber;
                    }
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
                    if (args is TargetSkippedEventArgs targetSkipped)
                    {
                        TargetSkipped(targetSkipped);
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
                    if (e is ProjectEvaluationStartedEventArgs projectEvaluationStarted)
                    {
                        var evaluationId = projectEvaluationStarted.BuildEventContext.EvaluationId;
                        var projectFilePath = Intern(projectEvaluationStarted.ProjectFile);
                        var projectName = Intern(Path.GetFileName(projectFilePath));
                        var nodeName = Intern(GetEvaluationProjectName(evaluationId, projectName));
                        var projectEvaluation = new ProjectEvaluation { Name = nodeName };
                        EvaluationFolder.AddChild(projectEvaluation);
                        projectEvaluation.ProjectFile = projectFilePath;

                        projectEvaluation.Id = evaluationId;
                        projectEvaluation.EvaluationText = Intern($"id:{evaluationId}");
                        projectEvaluation.NodeId = e.BuildEventContext.NodeId;
                        projectEvaluation.StartTime = e.Timestamp;
                        projectEvaluation.EndTime = e.Timestamp;
                    }
                    else if (e is ProjectEvaluationFinishedEventArgs projectEvaluationFinished)
                    {
                        var evaluationId = projectEvaluationFinished.BuildEventContext.EvaluationId;
                        var projectFilePath = Intern(projectEvaluationFinished.ProjectFile);
                        var projectName = Intern(Path.GetFileName(projectFilePath));
                        var nodeName = Intern(GetEvaluationProjectName(evaluationId, projectName));
                        var projectEvaluation = EvaluationFolder.FindLastChild<ProjectEvaluation, int>(static (e, evaluationId) => e.Id == evaluationId, evaluationId);
                        if (projectEvaluation == null)
                        {
                            // no matching ProjectEvaluationStarted
                            return;
                        }

                        projectEvaluation.EndTime = e.Timestamp;

                        var profilerResult = projectEvaluationFinished.ProfilerResult;
                        if (profilerResult != null && projectName != null)
                        {
                            ConstructProfilerResult(projectEvaluation, profilerResult.Value);
                        }

                        // Pre-create folder before starting the fill on the background thread.
                        Folder globFolder = null;
                        Folder itemsNode = null;
                        Folder propertiesFolder = null;
                        if (projectEvaluationFinished.GlobalProperties != null)
                        {
                            globFolder = GetOrCreateGlobalPropertiesFolder(projectEvaluation, projectEvaluationFinished.GlobalProperties);
                            globFolder.DisableChildrenCache = true;
                        }

                        if (projectEvaluationFinished.Items != null)
                        {
                            itemsNode = projectEvaluation.GetOrCreateNodeWithName<Folder>(Strings.Items, addAtBeginning: true);
                            itemsNode.DisableChildrenCache = true;
                        }

                        if (projectEvaluationFinished.Properties != null)
                        {
                            propertiesFolder = projectEvaluation.GetOrCreateNodeWithName<Folder>(Strings.Properties, addAtBeginning: true);
                            propertiesFolder.DisableChildrenCache = true;
                        }

                        Build.RunInBackground(() => AddGlobalProperties());

                        void AddGlobalProperties()
                        {
                            if (projectEvaluationFinished.GlobalProperties != null && globFolder != null)
                            {
                                AddProperties(globFolder, (IEnumerable<KeyValuePair<string, string>>)projectEvaluationFinished.GlobalProperties, project: projectEvaluation);
                            }

                            AddPropertiesSorted(propertiesFolder, projectEvaluation, projectEvaluationFinished.Properties);
                            AddItems(itemsNode, projectEvaluationFinished.Items);
                        }
                    } 
                    else if (e is BuildCanceledEventArgs buildCanceledEventArgs)
                    {
                        // If the build was canceled we want to show a message in the build log view.
                        messageProcessor.Process(new BuildMessageEventArgs(
                            Intern(buildCanceledEventArgs.Message),
                            Intern(buildCanceledEventArgs.HelpKeyword),
                            Intern(buildCanceledEventArgs.SenderName),
                            MessageImportance.High,
                            buildCanceledEventArgs.Timestamp));
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private static string GetEvaluationProjectName(int evaluationId, string projectName) => projectName;

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

                    var warning = new Warning();

                    string text = args.Message;

                    if (parent is ResolveAssemblyReferenceTask rar)
                    {
                        var match = Strings.IsFoundConflicts(text);
                        if (match.Success)
                        {
                            string details = match.Groups[2].Value;
                            // https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/443
                            ItemGroupParser.ParseThereWasAConflict(warning, details, stringTable);
                            text = text.GetFirstLine();
                        }
                    }

                    if (args.HelpKeyword == "MSBuild.DuplicateImport")
                    {
                        var import = parent.FindFirstDescendant<Import>(i =>
                            string.Equals(args.File, i.ImportedProjectFilePath, StringComparison.OrdinalIgnoreCase));
                        if (import != null)
                        {
                            parent = import;
                        }
                    }

                    if (parent is ProjectEvaluation)
                    {
                        parent = parent.GetOrCreateNodeWithName<Folder>(Strings.Warnings);
                    }

                    Populate(warning, args, text);

                    parent.AddChild(warning);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private TreeNode FindParent(BuildEventContext buildEventContext)
        {
            TreeNode result = null;

            if (buildEventContext.ProjectContextId == -2)
            {
                var evaluationId = buildEventContext.EvaluationId;

                result = EvaluationFolder;

                var projectEvaluation = result.FindChild<ProjectEvaluation, int>(static (p, evaluationId) => p.Id == evaluationId, evaluationId);
                if (projectEvaluation != null)
                {
                    result = projectEvaluation;
                }

                return result;
            }

            Project project = GetOrAddProject(buildEventContext.ProjectContextId);
            result = project;
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

                    var errors = parent.GetOrCreateNodeWithName<Folder>(Strings.Errors);
                    var error = new Error();
                    Populate(error, args);
                    errors.AddChild(error);

                    if (Build.FirstError == null)
                    {
                        Build.FirstError = error;
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void Populate(AbstractDiagnostic message, BuildWarningEventArgs args, string text)
        {
            message.Text = Intern(text);
            message.Timestamp = args.Timestamp;
            message.Code = Intern(args.Code);
            message.ColumnNumber = args.ColumnNumber;
            message.EndColumnNumber = args.EndColumnNumber;
            message.EndLineNumber = args.EndLineNumber;
            message.LineNumber = args.LineNumber;
            message.File = Intern(args.File);
            message.ProjectFile = Intern(args.ProjectFile);
            message.Subcategory = Intern(args.Subcategory);

            PopulateWithExtendedData(message, args);
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

            PopulateWithExtendedData(message, args);
        }

        internal static void PopulateWithExtendedData(TreeNode node, BuildEventArgs args)
        {
            if (args is not IExtendedBuildEventArgs extended)
            {
                return;
            }

            node.EnsureChildrenCapacity(
                (node.Children?.Count ?? 0)
                + 1 // For extended type name
                + (string.IsNullOrWhiteSpace(extended.ExtendedData) ? 0 : 1)
                + (extended.ExtendedMetadata?.Count > 0 ? 1 : 0));

            var typeNode = new Property
            {
                Name = "Type",
                Value = extended.ExtendedType
            };

            node.AddChild(typeNode);

            if (!string.IsNullOrWhiteSpace(extended.ExtendedData))
            {
                var dataNode = new Message
                {
                    Text = extended.ExtendedData,
                };

                node.AddChild(dataNode);
            }

            if (extended.ExtendedMetadata?.Count > 0)
            {
                var metadataFolder = new Folder
                {
                    Name = "Metadata"
                };
                metadataFolder.EnsureChildrenCapacity(extended.ExtendedMetadata.Count);

                foreach (KeyValuePair<string, string> kvp in extended.ExtendedMetadata)
                {
                    var metadataNode = new Metadata { Name = kvp.Key, Value = kvp.Value };
                    metadataFolder.AddChild(metadataNode);
                }

                node.AddChild(metadataFolder);
            }
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

        public Project GetProject(int projectId)
        {
            _projectIdToProjectMap.TryGetValue(projectId, out var result);
            return result;
        }

        public Target GetTarget(int projectId, int targetId)
        {
            if (!_projectIdToProjectMap.TryGetValue(projectId, out var project))
            {
                return null;
            }

            return project.GetTargetById(targetId);
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
                project.Name = Intern(Path.GetFileName(args.ProjectFile));
                project.ProjectFile = Intern(args.ProjectFile);
                project.EntryTargets = string.IsNullOrWhiteSpace(args.TargetNames)
                    ? ImmutableArray<string>.Empty
                    : stringTable.InternList(TextUtilities.SplitSemicolonDelimitedList(args.TargetNames));
                project.TargetsText = args.TargetNames;

                var evaluationId = BuildEventContext.InvalidEvaluationId;
                if (args.BuildEventContext.EvaluationId > BuildEventContext.InvalidEvaluationId)
                {
                    evaluationId = args.BuildEventContext.EvaluationId;
                }
                else if (args.ParentProjectBuildEventContext != null && args.ParentProjectBuildEventContext.EvaluationId > BuildEventContext.InvalidEvaluationId)
                {
                    evaluationId = args.ParentProjectBuildEventContext.EvaluationId;
                }

                project.EvaluationId = evaluationId;
                if (evaluationId != BuildEventContext.InvalidEvaluationId)
                {
                    project.EvaluationText = Intern("id:" + evaluationId);
                }

                project.GlobalProperties = stringTable.InternStringDictionary(args.GlobalProperties) ?? ImmutableDictionary<string, string>.Empty;

                // Pre-create folder before starting the fill on the background thread.
                Folder globalNode = null;
                if (args.GlobalProperties != null)
                {
                    globalNode = GetOrCreateGlobalPropertiesFolder(project, project.GlobalProperties);
                    globalNode.DisableChildrenCache = true;
                }

                Folder targetsNode = null;
                Folder itemFolder = null;
                Folder propertyFolder = null;
                if (!string.IsNullOrEmpty(args.TargetNames))
                {
                    targetsNode = project.GetOrCreateNodeWithName<Folder>(Strings.EntryTargets);
                    targetsNode.DisableChildrenCache = true;
                }

                if (args.Items != null)
                {
                    itemFolder = project.GetOrCreateNodeWithName<Folder>(Strings.Items, addAtBeginning: true);
                    itemFolder.DisableChildrenCache = true;
                }

                if (args.Properties != null)
                {
                    propertyFolder = project.GetOrCreateNodeWithName<Folder>(Strings.Properties, addAtBeginning: true);
                    propertyFolder.DisableChildrenCache = true;
                }

                Build.RunInBackground(() => AddGlobalProperties());

                void AddGlobalProperties()
                {
                    if (args.GlobalProperties != null && globalNode != null)
                    {
                        AddProperties(globalNode, args.GlobalProperties, project: project);
                    }

                    if (!string.IsNullOrEmpty(args.TargetNames))
                    {
                        AddEntryTargets(targetsNode, project);
                    }

                    AddPropertiesSorted(propertyFolder, project, args.Properties);
                    AddItems(itemFolder, args.Items);
                }
            }
        }

        public void AddMetadata(ITaskItem item, Item itemNode)
        {
            var cloned = item.CloneCustomMetadata();
            if (cloned is ArrayDictionary<string, string> metadata)
            {
                int count = metadata.Count;
                if (count == 0)
                {
                    return;
                }

                itemNode.EnsureChildrenCapacity(count);

                var keys = metadata.KeyArray;
                var values = metadata.ValueArray;

                for (int i = 0; i < count; i++)
                {
                    var key = keys[i];
                    var value = values[i];

                    var metadataNode = new Metadata
                    {
                        Name = key,
                        Value = value
                    };

                    // hot path, do not use AddChild
                    // itemNode.AddChild(metadataNode);
                    itemNode.Children.Add(metadataNode);
                    metadataNode.Parent = itemNode;
                }
            }
            else
            {
                if (cloned is ICollection collection)
                {
                    int count = collection.Count;
                    if (count == 0)
                    {
                        return;
                    }

                    itemNode.EnsureChildrenCapacity(count);
                }

                foreach (DictionaryEntry metadataName in cloned)
                {
                    var metadataNode = new Metadata
                    {
                        Name = SoftIntern(Convert.ToString(metadataName.Key)),
                        Value = SoftIntern(Convert.ToString(metadataName.Value))
                    };

                    itemNode.Children.Add(metadataNode);
                    metadataNode.Parent = itemNode;
                }
            }
        }

        public void AddItems(Folder itemsNode, IEnumerable itemList)
        {
            if (itemList == null)
            {
                return;
            }

            IEnumerable<DictionaryEntry> entries = itemList as IEnumerable<DictionaryEntry> ??
                itemList.Cast<DictionaryEntry>(); // this should be unreachable

            AddItem currentItemNode = null;

            foreach (DictionaryEntry kvp in entries)
            {
                var itemType = SoftIntern(Convert.ToString(kvp.Key));

                if (currentItemNode == null || currentItemNode.Name != itemType)
                {
                    currentItemNode = new AddItem { Name = itemType };
                    itemsNode.AddChild(currentItemNode);
                }

                var itemNode = new Item();

                if (kvp.Value is ITaskItem taskItem)
                {
                    itemNode.Text = SoftIntern(taskItem.ItemSpec);
                    AddMetadata(taskItem, itemNode);
                    currentItemNode.AddChild(itemNode);
                }
            }

            if (!IsLargeBinlog)
            {
                itemsNode.SortChildren();
            }
        }

        private void AddPropertiesSorted(Folder propertiesFolder, TreeNode project, IEnumerable properties)
        {
            if (properties == null)
            {
                return;
            }

            if (properties is IEnumerable<DictionaryEntry> entries)
            {
                properties = entries
                    .Select(e => new KeyValuePair<string, string>(Convert.ToString(e.Key), Convert.ToString(e.Value)))
                    .ToArray();
            }

            var list = (ICollection<KeyValuePair<string, string>>)properties;
            int count = list.Count;
            if (count == 0)
            {
                return;
            }

            IEnumerable<KeyValuePair<string, string>> sorted = list;

            if (list is ArrayDictionary<string, string> arrayDictionary)
            {
                // don't sort properties for large binlogs, this is very expensive
                if (!IsLargeBinlog)
                {
                    arrayDictionary.Sort();
                }
            }
            else
            {
                sorted = list.OrderBy(d => d.Key, StringComparer.OrdinalIgnoreCase);
            }

            AddProperties(
                propertiesFolder,
                sorted,
                count,
                project as IProjectOrEvaluation);
        }

        private static HashSet<string> ignoreAssemblyForTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AssignTargetPath",
            "CallTarget",
            "Copy",
            "Delete",
            "FindUnderPath",
            "MakeDir",
            "Message",
            "MSBuild",
            "ReadLinesFromFile",
            "WriteLinesToFile"
        };

        private Task CreateTask(TaskStartedEventArgs taskStartedEventArgs)
        {
            var taskName = Intern(taskStartedEventArgs.TaskName);

            string assembly = taskStartedEventArgs is TaskStartedEventArgs2 taskStartedEventArgs2 && !string.IsNullOrEmpty(taskStartedEventArgs2.TaskAssemblyLocation) ?
                taskStartedEventArgs2.TaskAssemblyLocation :
                GetTaskAssembly(taskName);

            var taskId = taskStartedEventArgs.BuildEventContext.TaskId;
            var startTime = taskStartedEventArgs.Timestamp;

            Task result = taskName.ToLowerInvariant() switch
            {
                "msbuild" => new MSBuildTask(),
                "resolveassemblyreference" => new ResolveAssemblyReferenceTask(),
                "copy" => new CopyTask(),
                "robocopy" => new RobocopyTask(),
                "csc" => new CscTask(),
                "vbc" => new VbcTask(),
                "fsc" => new FscTask(),
                "cl" => new CppAnalyzer.CppTask(),
                "lib" => new CppAnalyzer.CppTask(),
                "link" => new CppAnalyzer.CppTask(),
                "multitooltask" => new CppAnalyzer.CppTask(),
                _ => new Task(),
            };

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
            lock (_taskToAssemblyMap)
            {
                _taskToAssemblyMap.TryGetValue(taskName, out string assembly);

                // ignore built-in tasks from our Core assembly
                if (assembly == "Microsoft.Build.Tasks.Core, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" &&
                    ignoreAssemblyForTasks.Contains(taskName))
                {
                    assembly = null;
                }

                return assembly;
            }
        }

        /// <summary>
        /// Sets the assembly location for a given task.
        /// </summary>
        /// <param name="taskName">Name of the task.</param>
        /// <param name="assembly">The assembly location.</param>
        public void SetTaskAssembly(string taskName, string assembly)
        {
            lock (_taskToAssemblyMap)
            {
                // Important to overwrite because the Using task ... message is usually logged immediately before the TaskStarted
                // so need to make sure we remember the last assembly used for this task
                // see issue https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/669
                _taskToAssemblyMap[taskName] = Intern(assembly);
            }
        }

        private Folder GetOrCreateGlobalPropertiesFolder(TreeNode project, IEnumerable globalProperties)
        {
            if (globalProperties == null)
            {
                return null;
            }

            var propertiesNode = project.GetOrCreateNodeWithName<Folder>(Strings.Properties, addAtBeginning: true);
            var globalNode = propertiesNode.GetOrCreateNodeWithName<Folder>(Strings.Global, addAtBeginning: true);

            return globalNode;
        }

        private static void AddEntryTargets(Folder targetsNode, Project project)
        {
            var entryTargets = project.EntryTargets;
            if (entryTargets != null)
            {
                foreach (var entryTarget in entryTargets)
                {
                    var property = new EntryTarget
                    {
                        Name = entryTarget,
                    };
                    targetsNode.AddChild(property);
                }
            }
        }

        private void AddProperties(TreeNode parent, IEnumerable<KeyValuePair<string, string>> properties, int count = 0, IProjectOrEvaluation project = null)
        {
            if (properties == null)
            {
                return;
            }

            parent.DisableChildrenCache = true;

            if (count > 0)
            {
                parent.EnsureChildrenCapacity(count);
            }
            else if (properties is ICollection collection)
            {
                parent.EnsureChildrenCapacity(collection.Count);
            }

            bool tfvFound = false;
            bool platformFound = false;
            bool configFound = false;

            foreach (var kvp in properties)
            {
                var property = new Property
                {
                    Name = SoftIntern(kvp.Key),
                    Value = SoftIntern(kvp.Value)
                };

                parent.Children.Add(property); // don't use AddChild for performance
                property.Parent = parent;

                if (project != null)
                {
                    if (!tfvFound && string.Equals(kvp.Key, Strings.TargetFramework, StringComparison.OrdinalIgnoreCase))
                    {
                        project.TargetFramework = kvp.Value;
                        tfvFound = true;
                    }
                    else if (!tfvFound && string.Equals(kvp.Key, Strings.TargetFrameworks, StringComparison.OrdinalIgnoreCase))
                    {
                        // we want TargetFramework to take precedence over TargetFrameworks when both are present
                        if (string.IsNullOrEmpty(project.TargetFramework) && !string.IsNullOrEmpty(kvp.Value))
                        {
                            project.TargetFramework = kvp.Value;
                            tfvFound = true;
                        }
                    }
                    // If neither of the above are there - look for the old project system
                    else if (!tfvFound && project.TargetFramework is null && string.Equals(kvp.Key, Strings.TargetFrameworkVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        // Note this is untranslated, so e.g. "v4.6.2" instead of "net462" - this is intentional as it
                        // renders the badge for all projects, but you can still use this difference to tell what is/isn't an SDK project.
                        project.TargetFramework = kvp.Value;
                        tfvFound = true;
                    }
                    else if (!platformFound && string.Equals(kvp.Key, Strings.Platform, StringComparison.OrdinalIgnoreCase))
                    {
                        project.Platform = kvp.Value;
                        platformFound = true;
                    }
                    else if (!configFound && string.Equals(kvp.Key, Strings.Configuration, StringComparison.OrdinalIgnoreCase))
                    {
                        project.Configuration = kvp.Value;
                        configFound = true;
                    }
                }
            }
        }
    }
}
