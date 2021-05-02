﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Collections;
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

                string folderName = kind == TaskParameterMessageKind.TaskInput ? Strings.Parameters : Strings.OutputItems;
                parent = task.GetOrCreateNodeWithName<Folder>(folderName);

                node = CreateParameterNode(itemType, items);
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

        private BaseNode CreateParameterNode(string itemName, IEnumerable items)
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

            var parameter = new Parameter { Name = itemName };

            AddItems(items, parameter);

            return parameter;
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
                Construction.AddMetadata(item, itemNode);
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

            if (buildEventContext.TaskId > 0)
            {
                parent = GetTask(args);
                if (parent is Task task)
                {
                    if (task is ResolveAssemblyReferenceTask rar)
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
                }
            }
            else if (buildEventContext.TargetId > 0)
            {
                parent = GetTarget(args);

                if (Strings.TaskSkippedFalseConditionRegex.IsMatch(message))
                {
                    lowRelevance = true;
                }
            }
            else if (buildEventContext.ProjectContextId > 0)
            {
                var project = construction.GetOrAddProject(buildEventContext.ProjectContextId);
                parent = project;

                if (Strings.IsTargetSkipped(message))
                {
                    var targetName = Intern(TextUtilities.ParseQuotedSubstring(message));
                    if (targetName != null)
                    {
                        var skippedTarget = project.GetOrAddTargetByName(targetName, args.Timestamp);
                        skippedTarget.StartTime = args.Timestamp;
                        skippedTarget.EndTime = args.Timestamp;
                        parent = skippedTarget;
                        lowRelevance = true;
                    }
                }
            }
            else if (buildEventContext.EvaluationId != -1)
            {
                parent = construction.EvaluationFolder;

                var evaluationId = buildEventContext.EvaluationId;
                var evaluation = construction.Build.FindEvaluation(evaluationId);
                if (evaluation != null)
                {
                    parent = evaluation;
                }

                if (args is PropertyReassignmentEventArgs || Strings.PropertyReassignmentRegex.IsMatch(message))
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
                else if (construction.Build.FileFormatVersion < 9 && Strings.PropertyReassignmentRegex.IsMatch(message))
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
                    buildEventContext.NodeId == 0 &&
                    buildEventContext.ProjectContextId == 0 &&
                    buildEventContext.ProjectInstanceId == 0 &&
                    buildEventContext.TargetId == 0 &&
                    buildEventContext.TaskId == 0)
                {
                    // must be Detailed Build Summary
                    // https://github.com/Microsoft/msbuild/blob/master/src/XMakeBuildEngine/BackEnd/Components/Scheduler/Scheduler.cs#L509
                    DetailedSummary.AppendLine(message);
                    return;
                }
                else if (
                    buildEventContext.NodeId == -2 &&
                    buildEventContext.ProjectContextId == -2 &&
                    buildEventContext.ProjectInstanceId == -1)
                {
                    if (message.StartsWith(Strings.MSBuildVersionPrefix))
                    {
                        var version = message.Substring(Strings.MSBuildVersionPrefix.Length);
                        construction.Build.MSBuildVersion = version;
                    }
                }
            }

            if (nodeToAdd == null)
            {
                message = Intern(message);
                nodeToAdd = new Message
                {
                    Text = message,
                    Timestamp = args.Timestamp,
                    IsLowRelevance = lowRelevance
                };
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
                            if (equals != -1)
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

                var parameterName = Intern(message.TrimEnd(':'));
                var parameter = new Parameter
                {
                    Name = parameterName
                };

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
