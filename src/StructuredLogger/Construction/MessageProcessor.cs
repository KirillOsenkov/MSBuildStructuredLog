using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class MessageProcessor
    {
        private readonly Construction construction;
        private readonly StringCache stringTable;

        public StringBuilder DetailedSummary { get; } = new StringBuilder();

        public MessageProcessor(Construction construction, StringCache stringTable)
        {
            this.construction = construction;
            this.stringTable = stringTable;
        }

        private string Intern(string text) => stringTable.Intern(text);

        public void Process(BuildMessageEventArgs args)
        {
            if (args == null)
            {
                return;
            }

            if (args is TaskParameterEventArgs taskParameter)
            {
                ProcessTaskParameter(taskParameter);
                return;
            }
            else if (args is ProjectImportedEventArgs projectImported)
            {
                ProcessProjectImported(projectImported);
                return;
            }

            var message = args.Message;
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            var buildEventContext = args.BuildEventContext;
            if (buildEventContext != null && buildEventContext.TaskId != BuildEventContext.InvalidTaskId)
            {
                if (message.StartsWith(Strings.OutputItemsMessagePrefix, StringComparison.Ordinal))
                {
                    var task = GetTask(args);

                    //this.construction.Build.Statistics.ReportOutputItemMessage(task, message);

                    var folder = task.GetOrCreateNodeWithName<Folder>(Strings.OutputItems);
                    var parameter = ItemGroupParser.ParsePropertyOrItemList(message, Strings.OutputItemsMessagePrefix, stringTable, isOutputItem: true);
                    folder.AddChild(parameter);
                    return;
                }

                if (message.StartsWith(Strings.OutputPropertyMessagePrefix, StringComparison.Ordinal))
                {
                    var task = GetTask(args);
                    var folder = task.GetOrCreateNodeWithName<Folder>(Strings.OutputProperties);
                    var parameter = ItemGroupParser.ParsePropertyOrItemList(message, Strings.OutputPropertyMessagePrefix, stringTable);
                    folder.AddChild(parameter);
                    return;
                }

                if (message.StartsWith(Strings.TaskParameterMessagePrefix, StringComparison.Ordinal))
                {
                    var task = GetTask(args);
                    if (IgnoreParameters(task))
                    {
                        return;
                    }

                    //this.construction.Build.Statistics.ReportTaskParameterMessage(task, message);

                    var folder = task.GetOrCreateNodeWithName<Folder>(Strings.Parameters);
                    var parameter = ItemGroupParser.ParsePropertyOrItemList(message, Strings.TaskParameterMessagePrefix, stringTable);
                    folder.AddChild(parameter);
                    return;
                }

                if (args is TaskCommandLineEventArgs taskArgs)
                {
                    if (AddCommandLine(taskArgs))
                    {
                        return;
                    }
                }
            }
            else if (buildEventContext != null && buildEventContext.TargetId != BuildEventContext.InvalidTargetId)
            {
                if (message.StartsWith(Strings.ItemGroupIncludeMessagePrefix, StringComparison.Ordinal))
                {
                    AddItemGroup(args, message, Strings.ItemGroupIncludeMessagePrefix, new AddItem());
                    return;
                }

                if (message.StartsWith(Strings.ItemGroupRemoveMessagePrefix, StringComparison.Ordinal))
                {
                    AddItemGroup(args, message, Strings.ItemGroupRemoveMessagePrefix, new RemoveItem());
                    return;
                }

                if (message.StartsWith(Strings.PropertyGroupMessagePrefix, StringComparison.Ordinal))
                {
                    AddPropertyGroup(args, message, Strings.PropertyGroupMessagePrefix);
                    return;
                }

                // A task from assembly message (parses out the task name and assembly path).
                var match = Strings.UsingTask(message);
                if (match.Success)
                {
                    construction.SetTaskAssembly(
                        Intern(match.Groups["task"].Value),
                        Intern(match.Groups["assembly"].Value));
                    return;
                }
            }
            else if (buildEventContext != null && buildEventContext.EvaluationId != BuildEventContext.InvalidEvaluationId)
            {
            }
            else
            {
                if (args.SenderName == "BinaryLogger")
                {
                    var parameter = ItemGroupParser.ParsePropertyOrItemList(message, string.Empty, stringTable);
                    construction.Build.AddChild(parameter);
                    return;
                }
            }

            // Just the generic log message or something we currently don't handle in the object model.
            AddMessage(args, message);
        }

        private void ProcessProjectImported(ProjectImportedEventArgs args)
        {
            var import = ImportTreeAnalyzer.TryGetImportOrNoImport(args, stringTable);
            if (import == null)
            {
                return;
            }

            var evaluationId = args.BuildEventContext.EvaluationId;
            var evaluation = construction.Build.FindEvaluation(evaluationId);
            if (evaluation != null)
            {
                evaluation.AddImport(import);
            }
        }

        private void ProcessTaskParameter(TaskParameterEventArgs args)
        {
            string itemType = args.ItemType;
            var items = args.Items;
            var kind = args.Kind;

            NamedNode parent = null;
            BaseNode node = null;
            if (kind == TaskParameterMessageKind.TaskInput || kind == TaskParameterMessageKind.TaskOutput)
            {
                var task = GetTask(args);
                if (task == null || IgnoreParameters(task))
                {
                    return;
                }

                bool isOutput = kind == TaskParameterMessageKind.TaskOutput;

                string folderName = isOutput ? Strings.OutputItems : Strings.Parameters;
                parent = task.GetOrCreateNodeWithName<Folder>(folderName);

                node = CreateParameterNode(itemType, items, isOutput);
            }
            else if (
                kind == TaskParameterMessageKind.AddItem ||
                kind == TaskParameterMessageKind.RemoveItem ||
                kind == TaskParameterMessageKind.SkippedTargetInputs ||
                kind == TaskParameterMessageKind.SkippedTargetOutputs)
            {
                parent = GetTarget(args);

                NamedNode named;
                if (kind == TaskParameterMessageKind.AddItem)
                {
                    named = new AddItem
                    {
                        LineNumber = args.LineNumber
                    };
                }
                else if (kind == TaskParameterMessageKind.RemoveItem)
                {
                    named = new RemoveItem
                    {
                        LineNumber = args.LineNumber
                    };
                }
                else
                {
                    named = new Folder();
                    if (kind == TaskParameterMessageKind.SkippedTargetInputs)
                    {
                        itemType = Strings.Inputs;
                    }
                    else
                    {
                        itemType = Strings.Outputs;
                    }
                }

                named.Name = itemType;

                AddItems(items, named);
                node = named;
            }

            if (node != null && parent != null)
            {
                parent.AddChild(node);
            }
        }

        private BaseNode CreateParameterNode(string itemName, IEnumerable items, bool isOutput = false)
        {
            if (items is IList<ITaskItem> list && list.Count == 1 && list[0] is ITaskItem scalar && scalar.MetadataCount == 0)
            {
                var property = new Property
                {
                    Name = itemName,
                    Value = scalar.ItemSpec
                };
                return property;
            }

            TreeNode parent;
            if (isOutput)
            {
                parent = new AddItem { Name = itemName };
            }
            else
            {
                parent = new Parameter { Name = itemName };
            }

            AddItems(items, parent);

            return parent;
        }

        private void AddItems(IEnumerable items, TreeNode parent)
        {
            if (items is ICollection collection)
            {
                parent.EnsureChildrenCapacity(collection.Count);
            }

            foreach (ITaskItem item in items)
            {
                var itemNode = new Item { Text = item.ItemSpec };
                this.construction.AddMetadata(item, itemNode);
                parent.AddChild(itemNode);
            }
        }

        private bool IgnoreParameters(Task task)
        {
            string taskName = task.Name;
            if (taskName == "Message")
            {
                return true;
            }

            return false;
        }

        private Task GetTask(BuildEventArgs args) => GetTask(args.BuildEventContext);

        private BuildEventContext lastTaskBuildEventContext;
        private Task lastTask;

        private Task GetTask(BuildEventContext buildEventContext)
        {
            if (buildEventContext.EqualTo(lastTaskBuildEventContext))
            {
                return lastTask;
            }

            Target target = GetTarget(buildEventContext);
            if (target == null)
            {
                lastTaskBuildEventContext = null;
                return null;
            }

            var task = target.GetTaskById(buildEventContext.TaskId);
            lastTaskBuildEventContext = buildEventContext;
            lastTask = task;
            return task;
        }

        private Target GetTarget(BuildEventArgs args) => GetTarget(args.BuildEventContext);

        private Target GetTarget(BuildEventContext buildEventContext) =>
            construction.GetTarget(buildEventContext.ProjectContextId, buildEventContext.TargetId);

        /// <summary>
        /// Handles BuildMessage event when a property discovery/evaluation is logged.
        /// </summary>
        /// <param name="args">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        /// <param name="prefix">The prefix string.</param>
        public void AddPropertyGroup(BuildMessageEventArgs args, string message, string prefix)
        {
            message = message.Substring(prefix.Length);

            var target = GetTarget(args);

            var kvp = TextUtilities.ParseNameValue(message);
            var property = new Property
            {
                Name = Intern(kvp.Key),
                Value = Intern(kvp.Value)
            };
            target.AddChild(property);
        }

        /// <summary>
        /// Handles BuildMessage event when an ItemGroup discovery/evaluation is logged.
        /// </summary>
        /// <param name="args">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        /// <param name="prefix">The prefix string.</param>
        public void AddItemGroup(BuildMessageEventArgs args, string message, string prefix, NamedNode containerNode)
        {
            var target = GetTarget(args);

            var itemGroup = ItemGroupParser.ParsePropertyOrItemList(message, prefix, stringTable);
            if (itemGroup is Property property)
            {
                itemGroup = new Item
                {
                    Text = property.Value
                };
                containerNode.Name = property.Name;
                containerNode.AddChild(itemGroup);
            }
            else if (itemGroup is Parameter parameter)
            {
                containerNode.Name = parameter.Name;
                foreach (BaseNode child in parameter.Children)
                {
                    child.Parent = null;
                    containerNode.AddChild(child);
                }
            }

            if (target.LastChild is NamedNode last &&
                last.GetType() == containerNode.GetType() &&
                last.Name == containerNode.Name)
            {
                foreach (BaseNode child in containerNode.Children)
                {
                    child.Parent = null;
                    last.AddChild(child);
                }

                return;
            }

            target.AddChild(containerNode);
        }

        private HashSet<string> evaluationMessagesAlreadySeen = new HashSet<string>(StringComparer.Ordinal);

        private static readonly char[] space = { ' ' };

        /// <summary>
        /// Handles a generic BuildMessage event and assigns it to the appropriate logging node.
        /// </summary>
        /// <param name="args">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        public void AddMessage(LazyFormattedBuildEventArgs args, string message)
        {
            TreeNode parent = null;
            BaseNode nodeToAdd = null;
            bool lowRelevance = false;

            var buildEventContext = args.BuildEventContext;

            if (buildEventContext != null && buildEventContext.TaskId > 0)
            {
                parent = GetTask(args);
                if (parent is Task task)
                {
                    if (args is AssemblyLoadBuildEventArgs)
                    {
                        nodeToAdd = new Message() { Text = Intern(message), IsLowRelevance = lowRelevance };
                    }
                    else if (task is ResolveAssemblyReferenceTask rar)
                    {
                        if (ProcessRAR(rar, ref parent, message))
                        {
                            return;
                        }
                    }
                    else if (string.Equals(task.Name, "MSBuild", StringComparison.OrdinalIgnoreCase))
                    {
                        if (ProcessMSBuildTask(task, ref parent, ref nodeToAdd, message))
                        {
                            return;
                        }
                    }
                    else if (string.Equals(task.Name, "RestoreTask", StringComparison.OrdinalIgnoreCase))
                    {
                        if (ProcessRestoreTask(task, ref parent, message))
                        {
                            return;
                        }
                    }
                    else if (string.Equals(task.Name, "Mmp", StringComparison.OrdinalIgnoreCase))
                    {
                        if (ProcessMmp(task, ref parent, message))
                        {
                            return;
                        }
                    }
                }
            }
            else if (buildEventContext != null && buildEventContext.TargetId > 0)
            {
                parent = GetTarget(args);

                if (message.Contains(Strings.TaskSkippedFalseCondition) && Strings.TaskSkippedFalseConditionRegex.IsMatch(message))
                {
                    lowRelevance = true;
                }
            }
            else if (buildEventContext != null && buildEventContext.ProjectContextId > 0)
            {
                var project = construction.GetOrAddProject(buildEventContext.ProjectContextId);
                parent = project;

                if (message.Equals("Building with tools version \"Current\".", StringComparison.Ordinal))
                {
                    // this is useless so just drop it on the floor
                    return;
                }

                var targetSkipReason = Strings.GetTargetSkipReason(message);
                if (targetSkipReason != TargetSkipReason.None)
                {
                    // Target skipped was a simple message before this PR:
                    // https://github.com/dotnet/msbuild/pull/6402
                    var targetName = Intern(TextUtilities.ParseQuotedSubstring(message));
                    if (targetName != null)
                    {
                        var args2 = new TargetSkippedEventArgs(message);
                        args2.TargetName = targetName;
                        args2.BuildEventContext = args.BuildEventContext;
                        args2.SkipReason = targetSkipReason;
                        args2.OriginallySucceeded = targetSkipReason != TargetSkipReason.PreviouslyBuiltUnsuccessfully;
                        Reflector.BuildEventArgs_timestamp.SetValue(args2, args.Timestamp);
                        construction.TargetSkipped(args2);
                        return;
                    }
                }
            }
            else if (buildEventContext != null && buildEventContext.EvaluationId != -1)
            {
                parent = construction.EvaluationFolder;

                var evaluationId = buildEventContext.EvaluationId;
                var evaluation = construction.Build.FindEvaluation(evaluationId);
                if (evaluation != null)
                {
                    parent = evaluation;
                }

                if (args is PropertyReassignmentEventArgs || (message.Contains(Strings.PropertyReassignment) && Strings.PropertyReassignmentRegex.IsMatch(message)))
                {
                    TimedNode properties;
                    if (evaluation != null)
                    {
                        properties = evaluation.PropertyReassignmentFolder;
                    }
                    else
                    {
                        properties = parent.GetOrCreateNodeWithName<TimedNode>(Strings.PropertyReassignmentFolder, addAtBeginning: true);
                    }

                    var propertyName = Strings.GetPropertyName(message);
                    parent = properties.GetOrCreateNodeWithName<Folder>(propertyName);
                }
                else if (parent == evaluation && parent.FindChild<Message>(message) != null)
                {
                    // avoid duplicate messages
                    return;
                }
            }
            else if (args.Message.StartsWith(Strings.NodesReusal, StringComparison.Ordinal))
            {
                parent = construction.Build.GetOrCreateNodeWithName<Folder>(Strings.NodesManagementNode);
            }

            if (parent == null)
            {
                parent = construction.Build;

                if (construction.Build.FileFormatVersion < 9 && Strings.IsEvaluationMessage(message))
                {
                    if (!evaluationMessagesAlreadySeen.Add(message))
                    {
                        return;
                    }

                    parent = construction.EvaluationFolder;
                }
                else if (construction.Build.FileFormatVersion < 9 && message.Contains(Strings.PropertyReassignment) && Strings.PropertyReassignmentRegex.IsMatch(message))
                {
                    if (!evaluationMessagesAlreadySeen.Add(message))
                    {
                        return;
                    }

                    var properties = construction.EvaluationFolder.GetOrCreateNodeWithName<Folder>(Strings.PropertyReassignmentFolder);
                    parent = properties.GetOrCreateNodeWithName<Folder>(Strings.GetPropertyName(message));
                }
                else if (Strings.IsTargetDoesNotExistAndWillBeSkipped(message))
                {
                    var folder = construction.EvaluationFolder;
                    parent = folder;
                    lowRelevance = true;
                }
                else if (
                    buildEventContext != null &&
                    buildEventContext.NodeId == 0 &&
                    buildEventContext.ProjectContextId == 0 &&
                    buildEventContext.ProjectInstanceId == 0 &&
                    buildEventContext.TargetId == 0 &&
                    buildEventContext.TaskId == 0)
                {
                    // must be Detailed Build Summary
                    // https://github.com/dotnet/msbuild/blob/d797c48da13aaa4dc7ae440ed7603c990cd44317/src/Build/BackEnd/Components/Scheduler/Scheduler.cs#L546
                    // Make sure to trim it otherwise it takes forever to load for huge builds
                    // and at that data volume it's just not useful
                    if (DetailedSummary.Length < 20_000_000)
                    {
                        DetailedSummary.AppendLine(message);
                    }

                    return;
                }
                else if (
                    buildEventContext != null &&
                    buildEventContext.NodeId == -2 &&
                    buildEventContext.ProjectContextId == -2 &&
                    buildEventContext.ProjectInstanceId == -1)
                {
                    if (message.StartsWith(Strings.MSBuildVersionPrefix))
                    {
                        var version = message.Substring(Strings.MSBuildVersionPrefix.Length);
                        construction.Build.MSBuildVersion = version;
                    }
                    else if (message.StartsWith(Strings.MSBuildExecutablePathPrefix))
                    {
                        var executablePath = message.Substring(Strings.MSBuildExecutablePathPrefix.Length);
                        construction.Build.MSBuildExecutablePath = executablePath;
                    }
                }
            }

            if (args is EnvironmentVariableReadEventArgs envArgs)
            {
                string environmentVariableName = Intern(envArgs.EnvironmentVariableName);
                string environmentVariableValue = Intern(message);
                nodeToAdd = new Property { Name = environmentVariableName, Value = environmentVariableValue };
                construction.AddEnvironmentVariable(environmentVariableName, environmentVariableValue);
            }
            else if (nodeToAdd == null)
            {
                message = Intern(message);

                if (args is CriticalBuildMessageEventArgs criticalArgs)
                {
                    var critical = new CriticalBuildMessage();
                    critical.Text = message;
                    critical.Timestamp = args.Timestamp;
                    critical.Code = Intern(criticalArgs.Code);
                    critical.ColumnNumber = criticalArgs.ColumnNumber;
                    critical.EndColumnNumber = criticalArgs.EndColumnNumber;
                    critical.EndLineNumber = criticalArgs.EndLineNumber;
                    critical.LineNumber = criticalArgs.LineNumber;
                    critical.File = Intern(criticalArgs.File);
                    critical.ProjectFile = Intern(criticalArgs.ProjectFile);
                    critical.Subcategory = Intern(criticalArgs.Subcategory);

                    nodeToAdd = critical;
                }
                else if (parent is Task task && task is CppAnalyzer.CppTask)
                {
                    nodeToAdd = new TimedMessage
                    {
                        Text = message,
                        Timestamp = args.Timestamp,
                        IsLowRelevance = lowRelevance
                    };
                }
                else
                {
                    nodeToAdd = new Message
                    {
                        Text = message,
                        IsLowRelevance = lowRelevance
                    };
                }
            }

            parent.AddChild(nodeToAdd);
        }

        private bool ProcessRAR(ResolveAssemblyReferenceTask task, ref TreeNode node, string message)
        {
            Folder inputs = task.Inputs;
            Folder results = task.Results;
            node = results ?? inputs;

            if (message.StartsWith("    ", StringComparison.Ordinal))
            {
                message = message.Substring(4);

                var parameter = node?.FindLastChild<Parameter>();
                if (parameter != null)
                {
                    bool thereWasAConflict = Strings.IsThereWasAConflictPrefix(parameter.Name);
                    if (thereWasAConflict)
                    {
                        if (construction.Build.IsMSBuildVersionAtLeast(16, 9))
                        {
                            // https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/443
                            ItemGroupParser.ParseThereWasAConflict(parameter, message, stringTable);
                        }
                        else
                        {
                            HandleThereWasAConflict(parameter, message, stringTable);
                        }

                        return true;
                    }

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        node = parameter;

                        if (message.StartsWith("    ", StringComparison.Ordinal))
                        {
                            message = message.Substring(4);

                            var lastItem = parameter.FindLastChild<Item>();

                            // only indent if it's not a "For SearchPath..." message - that one needs to be directly under parameter
                            // also don't indent if it's under AssemblyFoldersEx in Results
                            if (lastItem != null &&
                                !Strings.ForSearchPathPrefix.IsMatch(message) &&
                                !parameter.Name.StartsWith("AssemblyFoldersEx", StringComparison.Ordinal))
                            {
                                node = lastItem;
                            }
                        }

                        if (!string.IsNullOrEmpty(message))
                        {
                            var equals = message.IndexOf('=');
                            if (equals != -1 && message.IndexOfFirstLineBreak() == -1)
                            {
                                var kvp = TextUtilities.ParseNameValue(message);
                                var metadata = new Metadata
                                {
                                    Name = Intern(kvp.Key.TrimEnd(space)),
                                    Value = Intern(kvp.Value.TrimStart(space))
                                };
                                node.Children.Add(metadata);
                                metadata.Parent = node;
                            }
                            else
                            {
                                node.AddChild(new Item
                                {
                                    Text = Intern(message)
                                });
                            }
                        }
                    }

                    return true;
                }
            }
            else
            {
                if (results == null)
                {
                    bool isResult = Strings.UnifiedPrimaryReferencePrefix.IsMatch(message) ||
                       Strings.PrimaryReferencePrefix.IsMatch(message) ||
                       Strings.DependencyPrefix.IsMatch(message) ||
                       Strings.UnifiedDependencyPrefix.IsMatch(message) ||
                       Strings.AssemblyFoldersExLocation.IsMatch(message) ||
                       Strings.IsThereWasAConflictPrefix(message);

                    if (isResult)
                    {
                        results = task.GetOrCreateNodeWithName<Folder>(Strings.Results);
                        task.Results = results;
                        node = results;
                    }
                    else
                    {
                        if (inputs == null)
                        {
                            inputs = task.GetOrCreateNodeWithName<Folder>(Strings.Inputs);
                            task.Inputs = inputs;
                        }

                        node = inputs;
                    }
                }
                else
                {
                    node = results;
                }

                var parameter = new Parameter();
                string parameterName;

                if (Strings.IsThereWasAConflictPrefix(message))
                {
                    ItemGroupParser.ParseThereWasAConflict(parameter, message, stringTable);
                    parameterName = message.GetFirstLine();
                }
                else
                {
                    parameterName = message.TrimEnd(':');
                }

                parameter.Name = Intern(parameterName);

                node.AddChild(parameter);
                return true;
            }

            return false;
        }

        private bool ProcessMSBuildTask(Task task, ref TreeNode node, ref BaseNode nodeToAdd, string message)
        {
            var additionalPropertiesMatch = Strings.AdditionalPropertiesPrefix.IsMatch(message);
            if (message.StartsWith(Strings.GlobalPropertiesPrefix, StringComparison.Ordinal) ||
                additionalPropertiesMatch ||
                Strings.OverridingGlobalPropertiesPrefix.IsMatch(message) ||
                message.StartsWith(Strings.RemovingPropertiesPrefix, StringComparison.Ordinal) ||
                Strings.RemovingProjectProperties.IsMatch(message))
            {
                if (additionalPropertiesMatch)
                {
                    node = node.GetOrCreateNodeWithName<Folder>(Strings.AdditionalProperties);
                }

                node.GetOrCreateNodeWithName<Folder>(message);
                return true;
            }

            node = node.FindLastChild<Folder>() ?? node;
            if (message.Length > 2 && message[0] == ' ' && message[1] == ' ')
            {
                if (node is Folder f && f.Name == Strings.AdditionalProperties)
                {
                    node = f.FindLastChild<Folder>() ?? node;
                }

                message = message.Substring(2);
            }

            var kvp = TextUtilities.ParseNameValue(message);
            if (kvp.Value == "")
            {
                nodeToAdd = new Item
                {
                    Text = Intern(kvp.Key)
                };
            }
            else
            {
                nodeToAdd = new Property
                {
                    Name = Intern(kvp.Key),
                    Value = Intern(kvp.Value)
                };
            }

            return false;
        }

        private bool ProcessRestoreTask(Task task, ref TreeNode node, string message)
        {
            Folder CreateFolder(TreeNode node, string name)
            {
                return node.GetOrCreateNodeWithName<Folder>(Intern(name));
            }

            // just throw these away to save space
            // https://github.com/NuGet/Home/issues/10383
            if (message.StartsWith(Strings.RestoreTask_CheckingCompatibilityFor, StringComparison.Ordinal))
            {
                return true;
            }

            else if (message.StartsWith("  GET", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "GET");
            }
            else if (message.StartsWith("  CACHE", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "CACHE");
            }
            else if (message.StartsWith("  OK", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "OK");
            }
            else if (message.StartsWith("  NotFound", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "NotFound");
            }
            else if (message.StartsWith("PackageSignatureVerificationLog:", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "PackageSignatureVerificationLog");
            }
            else if (message.StartsWith("Writing assets file to disk", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "Assets file");
            }
            else if (message.StartsWith("Writing cache file to disk", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "Cache file");
            }
            else if (message.StartsWith("Persisting dg to", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "dg file");
            }
            else if (message.StartsWith("Generating MSBuild file", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "MSBuild file");
            }
            else if (message.StartsWith("Lock not required", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "Lock not required");
            }
            else if (message.StartsWith("Installing", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "Installing");
            }
            else if (message.StartsWith("Restoring packages for", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "Restoring packages for");
            }
            else if (message.StartsWith("Reading project file", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "Reading project file");
            }
            else if (message.StartsWith("Scanning packages for", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "Scanning packages for");
            }
            else if (message.StartsWith("Merging in runtimes", StringComparison.Ordinal))
            {
                node = CreateFolder(node, "Merging in runtimes");
            }
            else if (
                message.StartsWith(Strings.RestoreTask_CheckingCompatibilityFor, StringComparison.Ordinal) ||
                message.StartsWith(Strings.RestoreTask_CheckingCompatibilityOfPackages, StringComparison.Ordinal) ||
                message.StartsWith(Strings.RestoreTask_AcquiringLockForTheInstallation, StringComparison.Ordinal) ||
                message.StartsWith(Strings.RestoreTask_AcquiredLockForTheInstallation, StringComparison.Ordinal) ||
                message.StartsWith(Strings.RestoreTask_CompletedInstallationOf, StringComparison.Ordinal) ||
                message.StartsWith(Strings.RestoreTask_ResolvingConflictsFor, StringComparison.Ordinal) ||
                message.StartsWith(Strings.RestoreTask_AllPackagesAndProjectsAreCompatible, StringComparison.Ordinal) ||
                message.StartsWith(Strings.RestoreTask_Committing, StringComparison.Ordinal)
                )
            {
                return true;
            }

            return false;
        }

        private Folder lastMmpFolder = null;

        private readonly string[] mmpTerminalPrefixes = new[]
        {
            "Adding native reference",
            "Did not add native reference",
            "Added assembly",
            "Target",
            "Copied",
            "Linking with the framework",
            "Linking (weakly)",
            "Adding Framework",
            "Adding Weak Framework",
            "Generating static registrar"
        };

        private bool ProcessMmp(Task task, ref TreeNode node, string message)
        {
            Folder CreateFolder(TreeNode node, string name)
            {
                return node.GetOrCreateNodeWithName<Folder>(Intern(name));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return true;
            }

            var leadingSpaces = TextUtilities.GetNumberOfLeadingSpaces(message);
            if (leadingSpaces == 0)
            {
                lastMmpFolder = null;

                if (message == "Provided arguments:")
                {
                    lastMmpFolder = CreateFolder(node, "Provided arguments:");
                    return true;
                }

                if (message.StartsWith("Loaded assembly"))
                {
                    var loaded = CreateFolder(node, "Loaded assembly");
                    loaded = CreateFolder(loaded, message);
                    lastMmpFolder = loaded;
                    return true;
                }

                foreach (var prefix in mmpTerminalPrefixes)
                {
                    if (message.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        node = CreateFolder(node, prefix);
                        return false;
                    }
                }

                lastMmpFolder = null;
                return false;
            }

            if (lastMmpFolder != null && (lastMmpFolder.Parent == node || lastMmpFolder.Parent.Parent == node))
            {
                node = lastMmpFolder;
            }

            return false;
        }

        public static void HandleThereWasAConflict(Parameter parameter, string message, StringCache stringTable)
        {
            var numberOfLeadingSpaces = TextUtilities.GetNumberOfLeadingSpaces(message);
            TreeNode node = parameter;
            switch (numberOfLeadingSpaces)
            {
                case 0:
                    parameter.AddChild(new Item()
                    {
                        Text = stringTable.Intern(message)
                    });
                    break;
                case 4:
                    node = parameter.LastChild as TreeNode ?? node;
                    Add(node, message, 4);
                    break;
                case 6:
                    {
                        if (parameter.LastChild is TreeNode item)
                        {
                            node = item;
                            if (item.LastChild is TreeNode item2)
                            {
                                node = item2;
                            }
                        }
                        Add(node, message, 6);
                    }
                    break;
                case 8:
                    {
                        if (parameter.LastChild is TreeNode item)
                        {
                            node = item;
                            if (item.LastChild is TreeNode item2)
                            {
                                node = item2;
                                if (item2.LastChild is TreeNode item3)
                                {
                                    node = item3;
                                }
                            }
                        }
                        Add(node, message, 8);
                    }
                    break;
                default:
                    Add(node, message, 0);
                    break;
            }

            void Add(TreeNode parent, string text, int spaces)
            {
                text = text.Substring(spaces);
                parent.AddChild(new Item
                {
                    Text = stringTable.Intern(text)
                });
            }
        }

        /// <summary>
        /// Handler for a TaskCommandLine log event. Sets the command line arguments on the appropriate task. 
        /// </summary>
        /// <param name="args">The <see cref="TaskCommandLineEventArgs"/> instance containing the event data.</param>
        public bool AddCommandLine(TaskCommandLineEventArgs args)
        {
            var buildEventContext = args.BuildEventContext;
            if (buildEventContext.ProjectContextId == BuildEventContext.InvalidProjectContextId ||
                buildEventContext.TargetId == BuildEventContext.InvalidTargetId ||
                buildEventContext.TaskId == BuildEventContext.InvalidTaskId)
            {
                return false;
            }

            // task can be null as per https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/136
            var task = GetTask(args);
            if (task != null && !string.IsNullOrEmpty(args.CommandLine))
            {
                string commandLine = Intern(args.CommandLine);

                // a ToolTask can issue multiple TaskCommandLineEventArgs if Execute() is called multiple times
                // see https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/624
                task.AddChild(new Property { Name = Strings.CommandLineArguments, Value = commandLine });

                if (task.CommandLineArguments == null)
                {
                    task.CommandLineArguments = commandLine;
                }

                return true;
            }

            return false;
        }
    }
}
