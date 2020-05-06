using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class MessageProcessor
    {
        public static string TaskParameterMessagePrefix = Strings.TaskParameterMessagePrefix;
        public static string OutputItemsMessagePrefix = Strings.OutputItemsMessagePrefix;
        public static string OutputPropertyMessagePrefix = Strings.OutputPropertyMessagePrefix;
        public static string PropertyGroupMessagePrefix = Strings.PropertyGroupMessagePrefix;
        public static string ItemGroupIncludeMessagePrefix = Strings.ItemGroupIncludeMessagePrefix;
        public static string ItemGroupRemoveMessagePrefix = Strings.ItemGroupRemoveMessagePrefix;

        private readonly Construction construction;
        private readonly StringCache stringTable;

        public StringBuilder DetailedSummary { get; } = new StringBuilder();

        public MessageProcessor(Construction construction, StringCache stringTable)
        {
            this.construction = construction;
            this.stringTable = stringTable;
        }

        private static Regex usingTaskRegex = Strings.UsingTaskRegex;

        public void Process(BuildMessageEventArgs args)
        {
            if (args == null)
            {
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

            switch (message[0])
            {
                case 'A':
                    if (message.StartsWith(ItemGroupIncludeMessagePrefix))
                    {
                        AddItemGroup(args, ItemGroupIncludeMessagePrefix, new AddItem());
                        return;
                    }
                    break;
                case 'O':
                    if (message.StartsWith(OutputItemsMessagePrefix))
                    {
                        var task = GetTask(args);

                        //this.construction.Build.Statistics.ReportOutputItemMessage(task, message);

                        var folder = task.GetOrCreateNodeWithName<Folder>("OutputItems");
                        var parameter = ItemGroupParser.ParsePropertyOrItemList(message, OutputItemsMessagePrefix, stringTable);
                        folder.AddChild(parameter);
                        return;
                    }

                    if (message.StartsWith(OutputPropertyMessagePrefix))
                    {
                        var task = GetTask(args);
                        var folder = task.GetOrCreateNodeWithName<Folder>("OutputProperties");
                        var parameter = ItemGroupParser.ParsePropertyOrItemList(message, OutputPropertyMessagePrefix, stringTable);
                        folder.AddChild(parameter);
                        return;
                    }
                    break;
                case 'R':
                    if (message.StartsWith(ItemGroupRemoveMessagePrefix))
                    {
                        AddItemGroup(args, ItemGroupRemoveMessagePrefix, new RemoveItem());
                        return;
                    }
                    break;
                case 'S':
                    if (message.StartsWith(PropertyGroupMessagePrefix))
                    {
                        AddPropertyGroup(args, PropertyGroupMessagePrefix);
                        return;
                    }
                    break;
                case 'T':
                    if (message.StartsWith(TaskParameterMessagePrefix))
                    {
                        var task = GetTask(args);
                        if (IgnoreParameters(task))
                        {
                            return;
                        }

                        //this.construction.Build.Statistics.ReportTaskParameterMessage(task, message);

                        var folder = task.GetOrCreateNodeWithName<Folder>(Strings.Parameters);
                        var parameter = ItemGroupParser.ParsePropertyOrItemList(message, TaskParameterMessagePrefix, stringTable);
                        folder.AddChild(parameter);
                        return;
                    }
                    break;
                case 'U':
                    // A task from assembly message (parses out the task name and assembly path).
                    var match = usingTaskRegex.Match(message);
                    if (match.Success)
                    {
                        construction.SetTaskAssembly(
                            stringTable.Intern(match.Groups["task"].Value),
                            stringTable.Intern(match.Groups["assembly"].Value));
                        return;
                    }

                    break;
                default:
                    break;
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

        private bool IgnoreParameters(Task task)
        {
            string taskName = task.Name;
            if (taskName == "Message")
            {
                return true;
            }

            return false;
        }

        private Task GetTask(BuildMessageEventArgs args)
        {
            var project = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(args.BuildEventContext.TargetId);
            var task = target.GetTaskById(args.BuildEventContext.TaskId);
            return task;
        }

        /// <summary>
        /// Handles BuildMessage event when a property discovery/evaluation is logged.
        /// </summary>
        /// <param name="args">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        /// <param name="prefix">The prefix string.</param>
        public void AddPropertyGroup(BuildMessageEventArgs args, string prefix)
        {
            string message = args.Message.Substring(prefix.Length);

            var project = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(args.BuildEventContext.TargetId);

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
            var project = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(args.BuildEventContext.TargetId);

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
                node = construction
                    .GetOrAddProject(args.BuildEventContext.ProjectContextId)
                    .GetTargetById(args.BuildEventContext.TargetId)
                    .GetTaskById(args.BuildEventContext.TaskId);
                var task = node as Task;
                if (task != null)
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
                                bool thereWasAConflict = parameter.ToString().StartsWith(Strings.ThereWasAConflictPrefix);
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
                                            !message.StartsWith(Strings.ForSearchPathPrefix) &&
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
                                bool isResult = message.StartsWith(Strings.UnifiedPrimaryReferencePrefix) ||
                                    message.StartsWith(Strings.PrimaryReferencePrefix) ||
                                    message.StartsWith(Strings.DependencyPrefix) ||
                                    message.StartsWith(Strings.UnifiedDependencyPrefix) ||
                                    message.StartsWith(Strings.AssemblyFoldersExLocation) ||
                                    message.StartsWith(Strings.ThereWasAConflictPrefix);

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
                    else if (task.Name == "MSBuild")
                    {
                        if (message.StartsWith(Strings.GlobalPropertiesPrefix) ||
                            message.StartsWith(Strings.AdditionalPropertiesPrefix) ||
                            message.StartsWith(Strings.OverridingGlobalPropertiesPrefix) ||
                            message.StartsWith(Strings.RemovingPropertiesPrefix))
                        {
                            node.GetOrCreateNodeWithName<Folder>(message);
                            return;
                        }

                        node = node.FindLastChild<Folder>();
                        if (message[0] == ' ' && message[1] == ' ')
                        {
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
                }
            }
            else if (args.BuildEventContext?.TargetId > 0)
            {
                node = construction
                    .GetOrAddProject(args.BuildEventContext.ProjectContextId)
                    .GetTargetById(args.BuildEventContext.TargetId);

                if (Strings.IsTaskSkipped(message))
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
                        node = project.GetOrAddTargetByName(targetName);
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

                if (Strings.IsPropertyReassignmentMessage(message))
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
                else if (Strings.IsPropertyReassignmentMessage(message))
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

            var project = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(args.BuildEventContext.TargetId);

            // task can be null as per https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/136
            var task = target.GetTaskById(args.BuildEventContext.TaskId);
            if (task != null)
            {
                task.CommandLineArguments = stringTable.Intern(args.CommandLine);
                return true;
            }

            return false;
        }
    }
}
