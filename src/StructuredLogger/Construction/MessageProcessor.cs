using System;
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

            var message = args.Message;
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (args.SenderName == "BinaryLogger")
            {
                var parameter = ItemGroupParser.ParsePropertyOrItemList(message, string.Empty, stringTable);
                construction.Build.AddChild(parameter);
                return;
            }

            if (message.StartsWith(Strings.ItemGroupIncludeMessagePrefix))
            {
                AddItemGroup(args, Strings.ItemGroupIncludeMessagePrefix, new AddItem());
                return;
            }

            if (message.StartsWith(Strings.OutputItemsMessagePrefix))
            {
                var task = GetTask(args);

                //this.construction.Build.Statistics.ReportOutputItemMessage(task, message);

                var folder = task.GetOrCreateNodeWithName<Folder>(Strings.OutputItems);
                var parameter = ItemGroupParser.ParsePropertyOrItemList(message, Strings.OutputItemsMessagePrefix, stringTable);
                folder.AddChild(parameter);
                return;
            }

            if (message.StartsWith(Strings.OutputPropertyMessagePrefix))
            {
                var task = GetTask(args);
                var folder = task.GetOrCreateNodeWithName<Folder>(Strings.OutputProperties);
                var parameter = ItemGroupParser.ParsePropertyOrItemList(message, Strings.OutputPropertyMessagePrefix, stringTable);
                folder.AddChild(parameter);
                return;
            }

            if (message.StartsWith(Strings.ItemGroupRemoveMessagePrefix))
            {
                AddItemGroup(args, Strings.ItemGroupRemoveMessagePrefix, new RemoveItem());
                return;
            }

            if (message.StartsWith(Strings.PropertyGroupMessagePrefix))
            {
                AddPropertyGroup(args, Strings.PropertyGroupMessagePrefix);
                return;
            }
    
            if (message.StartsWith(Strings.TaskParameterMessagePrefix))
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

            // A task from assembly message (parses out the task name and assembly path).
            var match = Strings.UsingTask(message);
            if (match.Success)
            {
                construction.SetTaskAssembly(
                    stringTable.Intern(match.Groups["task"].Value),
                    stringTable.Intern(match.Groups["assembly"].Value));
                return;
            }

            if (args is TaskCommandLineEventArgs taskArgs)
            {
                if (AddCommandLine(taskArgs))
                {
                    return;
                }
            }

            // Just the generic log message or something we currently don't handle in the object model.
            AddMessage(args, message);
        }

        private void ProcessTaskParameter(TaskParameterEventArgs args)
        {
            string itemName = args.ItemName;
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

                node = CreateParameterNode(itemName, items);
            }
            else if (args.Kind == TaskParameterMessageKind.AddItem || args.Kind == TaskParameterMessageKind.RemoveItem)
            {
                parent = GetTarget(args);

                NamedNode named;
                if (args.Kind == TaskParameterMessageKind.AddItem)
                {
                    named = new AddItem();
                }
                else
                {
                    named = new RemoveItem();
                }

                named.Name = itemName;

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
                    Name = stringTable.Intern(itemName),
                    Value = stringTable.Intern(scalar.ItemSpec)
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
                foreach (string metadataName in item.MetadataNames)
                {
                    var value = item.GetMetadata(metadataName);
                    var metadataNode = new Metadata
                    {
                        Name = stringTable.Intern(metadataName),
                        Value = stringTable.Intern(value)
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
                Name = stringTable.Intern(kvp.Key),
                Value = stringTable.Intern(kvp.Value)
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
            message = stringTable.Intern(message);

            TreeNode node = null;
            var messageNode = new Message
            {
                Text = message,
                Timestamp = args.Timestamp
            };
            BaseNode nodeToAdd = messageNode;

            if (args.BuildEventContext?.TaskId > 0)
            {
                node = GetTask(args);
                if (node is Task task)
                {
                    if (task.Name == "ResolveAssemblyReference")
                    {
                        Folder inputs = task.GetOrCreateNodeWithName<Folder>("Inputs");
                        Folder results = task.FindChild<Folder>("Results");
                        node = results ?? inputs;

                        if (message.StartsWith("    "))
                        {
                            message = message.Substring(4);

                            var parameter = node.FindLastChild<Parameter>();
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

                                    if (message.StartsWith("    "))
                                    {
                                        message = message.Substring(4);

                                        var lastItem = parameter.FindLastChild<Item>();

                                        // only indent if it's not a "For SearchPath..." message - that one needs to be directly under parameter
                                        // also don't indent if it's under AssemblyFoldersEx in Results
                                        if (lastItem != null &&
                                            !Strings.ForSearchPathPrefix.IsMatch(message) &&
                                            !parameter.Name.StartsWith("AssemblyFoldersEx"))
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
                                                Name = stringTable.Intern(kvp.Key.TrimEnd(space)),
                                                Value = stringTable.Intern(kvp.Value.TrimStart(space))
                                            });
                                        }
                                        else
                                        {
                                            node.AddChild(new Item()
                                            {
                                                Text = stringTable.Intern(message)
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
                                    results = task.GetOrCreateNodeWithName<Folder>("Results");
                                    node = results;
                                }
                                else
                                {
                                    node = inputs;
                                }
                            }
                            else
                            {
                                node = results;
                            }

                            node.GetOrCreateNodeWithName<Parameter>(stringTable.Intern(message.TrimEnd(':')));
                            return;
                        }
                    }
                    else if (string.Equals(task.Name, "MSBuild", StringComparison.OrdinalIgnoreCase))
                    {
                        var additionalPropertiesMatch = Strings.AdditionalPropertiesPrefix.Match(message);
                        if (message.StartsWith(Strings.GlobalPropertiesPrefix) ||
                            additionalPropertiesMatch.Success ||
                            Strings.OverridingGlobalPropertiesPrefix.IsMatch(message) ||
                            message.StartsWith(Strings.RemovingPropertiesPrefix) ||
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
                                Text = stringTable.Intern(kvp.Key)
                            };
                        }
                        else
                        {
                            nodeToAdd = new Property
                            {
                                Name = stringTable.Intern(kvp.Key),
                                Value = stringTable.Intern(kvp.Value)
                            };
                        }
                    }
                    else if (string.Equals(task.Name, "RestoreTask"))
                    {
                        // just throw these away to save space
                        // https://github.com/NuGet/Home/issues/10383
                        if (message.StartsWith(Strings.RestoreTask_CheckingCompatibilityFor))
                        {
                            return;
                        }
                        else if (message.StartsWith("  GET"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("GET");
                        }
                        else if (message.StartsWith("  CACHE"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("CACHE");
                        }
                        else if (message.StartsWith("  OK"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("OK");
                        }
                        else if (message.StartsWith("  NotFound"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("NotFound");
                        }
                        else if (message.StartsWith("PackageSignatureVerificationLog:"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("PackageSignatureVerificationLog");
                        }
                        else if (message.StartsWith("Writing assets file to disk"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Assets file");
                        }
                        else if (message.StartsWith("Writing cache file to disk"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Cache file");
                        }
                        else if (message.StartsWith("Persisting dg to"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("dg file");
                        }
                        else if (message.StartsWith("Generating MSBuild file"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("MSBuild file");
                        }
                        else if (message.StartsWith("Lock not required"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Lock not required");
                        }
                        else if (message.StartsWith("Installing"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Installing");
                        }
                        else if (message.StartsWith("Restoring packages for"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Restoring packages for");
                        }
                        else if (message.StartsWith("Reading project file"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Reading project file");
                        }
                        else if (message.StartsWith("Scanning packages for"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Scanning packages for");
                        }
                        else if (message.StartsWith("Merging in runtimes"))
                        {
                            node = node.GetOrCreateNodeWithName<Folder>("Merging in runtimes");
                        }
                        else if (
                            message.StartsWith(Strings.RestoreTask_CheckingCompatibilityFor) ||
                            message.StartsWith(Strings.RestoreTask_CheckingCompatibilityOfPackages) ||
                            message.StartsWith(Strings.RestoreTask_AcquiringLockForTheInstallation) ||
                            message.StartsWith(Strings.RestoreTask_AcquiredLockForTheInstallation) ||
                            message.StartsWith(Strings.RestoreTask_CompletedInstallationOf) ||
                            message.StartsWith(Strings.RestoreTask_ResolvingConflictsFor) ||
                            message.StartsWith(Strings.RestoreTask_AllPackagesAndProjectsAreCompatible) ||
                            message.StartsWith(Strings.RestoreTask_Committing)
                            )
                        {
                            return;
                        }
                    }
                }
            }
            else if (args.BuildEventContext?.TargetId > 0)
            {
                node = GetTarget(args);

                if (Strings.TaskSkippedFalseCondition.Match(message).Success)
                {
                    messageNode.IsLowRelevance = true;
                }
            }
            else if (args.BuildEventContext?.ProjectContextId > 0)
            {
                var project = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId);
                node = project;

                if (Strings.IsTargetSkipped(message))
                {
                    var targetName = stringTable.Intern(TextUtilities.ParseQuotedSubstring(message));
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
            else if (args.BuildEventContext.EvaluationId != -1)
            {
                node = construction.EvaluationFolder;

                var project = node.FindChild<ProjectEvaluation>(p => p.Id == args.BuildEventContext.EvaluationId);
                if (project != null)
                {
                    node = project;
                }

                if (Strings.PropertyReassignment.IsMatch(message))
                {
                    var properties = node.GetOrCreateNodeWithName<Folder>(Strings.Properties, true);
                    node = properties.GetOrCreateNodeWithName<Folder>(Strings.GetPropertyName(message));
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
                else if (Strings.PropertyReassignment.IsMatch(message))
                {
                    if (!evaluationMessagesAlreadySeen.Add(message))
                    {
                        return;
                    }

                    var properties = construction.EvaluationFolder.GetOrCreateNodeWithName<Folder>(Strings.Properties);
                    node = properties.GetOrCreateNodeWithName<Folder>(Strings.GetPropertyName(message));
                }
                else if (Strings.IsTargetDoesNotExistAndWillBeSkipped(message))
                {
                    var folder = construction.EvaluationFolder;
                    node = folder;
                    messageNode.IsLowRelevance = true;
                }
                else if (args.BuildEventContext != null && (args.BuildEventContext.NodeId == 0 &&
                       args.BuildEventContext.ProjectContextId == 0 &&
                       args.BuildEventContext.ProjectInstanceId == 0 &&
                       args.BuildEventContext.TargetId == 0 &&
                       args.BuildEventContext.TaskId == 0))
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
                task.CommandLineArguments = stringTable.Intern(args.CommandLine);
                return true;
            }

            return false;
        }
    }
}
