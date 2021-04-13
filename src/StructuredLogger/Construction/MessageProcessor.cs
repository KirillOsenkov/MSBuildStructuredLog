using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

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
            if (buildEventContext.TaskId != BuildEventContext.InvalidTaskId)
            {
                if (message.StartsWith(Strings.OutputItemsMessagePrefix, StringComparison.Ordinal))
                {
                    var task = GetTask(args);

                    //this.construction.Build.Statistics.ReportOutputItemMessage(task, message);

                    var folder = task.GetOrCreateNodeWithName<Folder>(Strings.OutputItems);
                    var parameter = ItemGroupParser.ParsePropertyOrItemList(message, Strings.OutputItemsMessagePrefix, stringTable);
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
            else if (buildEventContext.TargetId != BuildEventContext.InvalidTargetId)
            {
                if (message.StartsWith(Strings.ItemGroupIncludeMessagePrefix, StringComparison.Ordinal))
                {
                    AddItemGroup(args, Strings.ItemGroupIncludeMessagePrefix, new AddItem());
                    return;
                }

                if (message.StartsWith(Strings.ItemGroupRemoveMessagePrefix, StringComparison.Ordinal))
                {
                    AddItemGroup(args, Strings.ItemGroupRemoveMessagePrefix, new RemoveItem());
                    return;
                }

                if (message.StartsWith(Strings.PropertyGroupMessagePrefix, StringComparison.Ordinal))
                {
                    AddPropertyGroup(args, Strings.PropertyGroupMessagePrefix);
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
            else if (buildEventContext.EvaluationId != BuildEventContext.InvalidEvaluationId)
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
            var items = args.Items.OfType<ITaskItem>().ToArray();

            NamedNode parent = null;
            BaseNode node = null;
            if (args.Kind == TaskParameterMessageKind.TaskInput || args.Kind == TaskParameterMessageKind.TaskOutput)
            {
                var task = GetTask(args);
                if (task == null || IgnoreParameters(task))
                {
                    return;
                }

                string folderName = args.Kind == TaskParameterMessageKind.TaskInput ? Strings.Parameters : Strings.OutputItems;
                parent = task.GetOrCreateNodeWithName<Folder>(folderName);

                node = CreateParameterNode(itemType, items);
            }
            else if (
                args.Kind == TaskParameterMessageKind.AddItem || 
                args.Kind == TaskParameterMessageKind.RemoveItem ||
                args.Kind == TaskParameterMessageKind.SkippedTargetInputs ||
                args.Kind == TaskParameterMessageKind.SkippedTargetOutputs)
            {
                parent = GetTarget(args);

                NamedNode named;
                if (args.Kind == TaskParameterMessageKind.AddItem)
                {
                    named = new AddItem();
                }
                else if (args.Kind == TaskParameterMessageKind.RemoveItem)
                {
                    named = new RemoveItem();
                }
                else
                {
                    named = new Folder();
                    if (args.Kind == TaskParameterMessageKind.SkippedTargetInputs)
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

        private BaseNode CreateParameterNode(string itemName, ITaskItem[] items)
        {
            if (items.Length == 1 && items[0] is ITaskItem scalar && scalar.MetadataCount == 0)
            {
                var property = new Property
                {
                    Name = itemName,
                    Value = Intern(scalar.ItemSpec)
                };
                return property;
            }

            var parameter = new Parameter { Name = itemName };

            AddItems(items, parameter);

            return parameter;
        }

        private void AddItems(ITaskItem[] items, TreeNode parent)
        {
            foreach (var item in items)
            {
                var itemNode = new Item { Text = item.ItemSpec };

                var metadata = item.CloneCustomMetadata();
                foreach (DictionaryEntry kvp in metadata)
                {
                    var metadataNode = new Metadata
                    {
                        Name = (string)kvp.Key,
                        Value = (string)kvp.Value
                    };
                    itemNode.AddChild(metadataNode);
                }

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

        private Task GetTask(BuildEventContext buildEventContext)
        {
            Target target = GetTarget(buildEventContext);
            if (target == null)
            {
                return null;
            }

            var task = target.GetTaskById(buildEventContext.TaskId);
            return task;
        }

        private Target GetTarget(BuildEventArgs args) => GetTarget(args.BuildEventContext);

        private Target GetTarget(BuildEventContext buildEventContext)
        {
            var project = construction.GetOrAddProject(buildEventContext.ProjectContextId);
            if (project == null)
            {
                return null;
            }

            var target = project.GetTargetById(buildEventContext.TargetId);
            return target;
        }

        /// <summary>
        /// Handles BuildMessage event when a property discovery/evaluation is logged.
        /// </summary>
        /// <param name="args">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        /// <param name="prefix">The prefix string.</param>
        public void AddPropertyGroup(BuildMessageEventArgs args, string prefix)
        {
            string message = args.Message.Substring(prefix.Length);

            var target = GetTarget(args);

            var kvp = TextUtilities.ParseNameValue(message);
            target.AddChild(new Property
            {
                Name = Intern(kvp.Key),
                Value = Intern(kvp.Value)
            });
        }

        /// <summary>
        /// Handles BuildMessage event when an ItemGroup discovery/evaluation is logged.
        /// </summary>
        /// <param name="args">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        /// <param name="prefix">The prefix string.</param>
        public void AddItemGroup(BuildMessageEventArgs args, string prefix, NamedNode containerNode)
        {
            var target = GetTarget(args);

            var itemGroup = ItemGroupParser.ParsePropertyOrItemList(args.Message, prefix, stringTable);
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
            message = Intern(message);

            TreeNode node = null;
            var messageNode = new Message
            {
                Text = message,
                Timestamp = args.Timestamp
            };
            BaseNode nodeToAdd = messageNode;

            var buildEventContext = args.BuildEventContext;

            if (buildEventContext?.TaskId > 0)
            {
                node = GetTask(args);
                if (node is Task task)
                {
                    if (task.Name == "ResolveAssemblyReference")
                    {
                        Folder inputs = task.FindChild<Folder>(Strings.Inputs);
                        Folder results = task.FindChild<Folder>(Strings.Results);
                        node = results ?? inputs;

                        if (message.StartsWith("    ", StringComparison.Ordinal))
                        {
                            message = message.Substring(4);

                            var parameter = node?.FindLastChild<Parameter>();
                            if (parameter != null)
                            {
                                bool thereWasAConflict = Strings.IsThereWasAConflictPrefix(parameter.ToString()); //parameter.ToString().StartsWith(Strings.ThereWasAConflictPrefix);
                                if (thereWasAConflict)
                                {
                                    HandleThereWasAConflict(parameter, message, stringTable);
                                    return;
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
                                        if (equals != -1)
                                        {
                                            var kvp = TextUtilities.ParseNameValue(message);
                                            node.AddChild(new Metadata
                                            {
                                                Name = Intern(kvp.Key.TrimEnd(space)),
                                                Value = Intern(kvp.Value.TrimStart(space))
                                            });
                                        }
                                        else
                                        {
                                            node.AddChild(new Item()
                                            {
                                                Text = Intern(message)
                                            });
                                        }
                                    }
                                }

                                return;
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
                                    node = results;
                                }
                                else
                                {
                                    if (inputs == null)
                                    {
                                        inputs = task.GetOrCreateNodeWithName<Folder>(Strings.Inputs);
                                    }

                                    node = inputs;
                                }
                            }
                            else
                            {
                                node = results;
                            }

                            node.GetOrCreateNodeWithName<Parameter>(Intern(message.TrimEnd(':')));
                            return;
                        }
                    }
                    else if (string.Equals(task.Name, "MSBuild", StringComparison.OrdinalIgnoreCase))
                    {
                        var additionalPropertiesMatch = Strings.AdditionalPropertiesPrefix.Match(message);
                        if (message.StartsWith(Strings.GlobalPropertiesPrefix, StringComparison.Ordinal) ||
                            additionalPropertiesMatch.Success ||
                            Strings.OverridingGlobalPropertiesPrefix.IsMatch(message) ||
                            message.StartsWith(Strings.RemovingPropertiesPrefix, StringComparison.Ordinal) ||
                            Strings.RemovingProjectProperties.IsMatch(message))
                        {
                            if (additionalPropertiesMatch.Success)
                            {
                                node = node.GetOrCreateNodeWithName<Folder>(Strings.AdditionalProperties);
                            }

                            node.GetOrCreateNodeWithName<Folder>(message);
                            return;
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
                    }
                    else if (string.Equals(task.Name, "RestoreTask"))
                    {
                        // just throw these away to save space
                        // https://github.com/NuGet/Home/issues/10383
                        if (message.StartsWith(Strings.RestoreTask_CheckingCompatibilityFor, StringComparison.Ordinal))
                        {
                            return;
                        }
                        else if (message.StartsWith("  GET", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("GET");
                        }
                        else if (message.StartsWith("  CACHE", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("CACHE");
                        }
                        else if (message.StartsWith("  OK", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("OK");
                        }
                        else if (message.StartsWith("  NotFound", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("NotFound");
                        }
                        else if (message.StartsWith("PackageSignatureVerificationLog:", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("PackageSignatureVerificationLog");
                        }
                        else if (message.StartsWith("Writing assets file to disk", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Assets file");
                        }
                        else if (message.StartsWith("Writing cache file to disk", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Cache file");
                        }
                        else if (message.StartsWith("Persisting dg to", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("dg file");
                        }
                        else if (message.StartsWith("Generating MSBuild file", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("MSBuild file");
                        }
                        else if (message.StartsWith("Lock not required", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Lock not required");
                        }
                        else if (message.StartsWith("Installing", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Installing");
                        }
                        else if (message.StartsWith("Restoring packages for", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Restoring packages for");
                        }
                        else if (message.StartsWith("Reading project file", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Reading project file");
                        }
                        else if (message.StartsWith("Scanning packages for", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Scanning packages for");
                        }
                        else if (message.StartsWith("Merging in runtimes", StringComparison.Ordinal))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Merging in runtimes");
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
                            return;
                        }
                    }
                }
            }
            else if (buildEventContext?.TargetId > 0)
            {
                node = GetTarget(args);

                if (Strings.TaskSkippedFalseCondition.Match(message).Success)
                {
                    messageNode.IsLowRelevance = true;
                }
            }
            else if (buildEventContext?.ProjectContextId > 0)
            {
                var project = construction.GetOrAddProject(buildEventContext.ProjectContextId);
                node = project;

                if (Strings.IsTargetSkipped(message))
                {
                    var targetName = Intern(TextUtilities.ParseQuotedSubstring(message));
                    if (targetName != null)
                    {
                        var skippedTarget = project.GetOrAddTargetByName(targetName, args.Timestamp);
                        skippedTarget.StartTime = args.Timestamp;
                        skippedTarget.EndTime = args.Timestamp;
                        node = skippedTarget;
                        messageNode.IsLowRelevance = true;
                    }
                }
            }
            else if (buildEventContext.EvaluationId != -1)
            {
                node = construction.EvaluationFolder;

                var evaluationId = buildEventContext.EvaluationId;
                var evaluation = construction.Build.FindEvaluation(evaluationId);
                if (evaluation != null)
                {
                    node = evaluation;
                }

                if (Strings.PropertyReassignmentRegex.IsMatch(message))
                {
                    TimedNode properties;
                    if (evaluation != null)
                    {
                        properties = evaluation.PropertyReassignmentFolder;
                    }
                    else
                    {
                        properties = node.GetOrCreateNodeWithName<TimedNode>(Strings.PropertyReassignmentFolder, addAtBeginning: true);
                    }

                    var propertyName = Strings.GetPropertyName(message);
                    node = properties.GetOrCreateNodeWithName<Folder>(propertyName);
                }

                if (node != null && node.FindChild<Message>(message) != null)
                {
                    // avoid duplicate messages
                    return;
                }
            }

            if (node == null)
            {
                node = construction.Build;

                if (Strings.IsEvaluationMessage(message))
                {
                    if (!evaluationMessagesAlreadySeen.Add(message))
                    {
                        return;
                    }

                    node = construction.EvaluationFolder;
                }
                else if (Strings.PropertyReassignmentRegex.IsMatch(message))
                {
                    if (!evaluationMessagesAlreadySeen.Add(message))
                    {
                        return;
                    }

                    var properties = construction.EvaluationFolder.GetOrCreateNodeWithName<Folder>(Strings.PropertyReassignmentFolder);
                    node = properties.GetOrCreateNodeWithName<Folder>(Strings.GetPropertyName(message));
                }
                else if (Strings.IsTargetDoesNotExistAndWillBeSkipped(message))
                {
                    var folder = construction.EvaluationFolder;
                    node = folder;
                    messageNode.IsLowRelevance = true;
                }
                else if (buildEventContext != null && (buildEventContext.NodeId == 0 &&
                       buildEventContext.ProjectContextId == 0 &&
                       buildEventContext.ProjectInstanceId == 0 &&
                       buildEventContext.TargetId == 0 &&
                       buildEventContext.TaskId == 0))
                {
                    // must be Detailed Build Summary
                    // https://github.com/Microsoft/msbuild/blob/master/src/XMakeBuildEngine/BackEnd/Components/Scheduler/Scheduler.cs#L509
                    DetailedSummary.AppendLine(message);
                    return;
                }
            }

            node.AddChild(nodeToAdd);
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
            if (task != null)
            {
                task.CommandLineArguments = Intern(args.CommandLine);
                return true;
            }

            return false;
        }
    }
}
