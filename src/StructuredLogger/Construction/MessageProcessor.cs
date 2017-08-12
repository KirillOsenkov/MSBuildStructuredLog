using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class MessageProcessor
    {
        public const string TaskParameterMessagePrefix = @"Task Parameter:";
        public const string OutputItemsMessagePrefix = @"Output Item(s): ";
        public const string OutputPropertyMessagePrefix = @"Output Property: ";
        public const string PropertyGroupMessagePrefix = @"Set Property: ";
        public const string ItemGroupIncludeMessagePrefix = @"Added Item(s): ";
        public const string ItemGroupRemoveMessagePrefix = @"Removed Item(s): ";

        private readonly Construction construction;
        private readonly StringCache stringTable;

        public MessageProcessor(Construction construction, StringCache stringTable)
        {
            this.construction = construction;
            this.stringTable = stringTable;
        }

        private static Regex usingTaskRegex = new Regex("Using \"(?<task>.+)\" task from (assembly|the task factory) \"(?<assembly>.+)\"\\.", RegexOptions.Compiled);

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
                        var folder = task.GetOrCreateNodeWithName<Folder>("Parameters");
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
                AddCommandLine(taskArgs);
                return;
            }

            // Just the generic log message or something we currently don't handle in the object model.
            AddMessage(args, message);
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

            var kvp = Utilities.ParseNameValue(message);
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
            var property = itemGroup as Property;
            if (property != null)
            {
                itemGroup = new Item
                {
                    Name = property.Name,
                    Text = property.Value
                };
                containerNode.Name = stringTable.Intern(property.Name);
            }

            containerNode.AddChild(itemGroup);
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
            object nodeToAdd = messageNode;

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
                                if (!string.IsNullOrWhiteSpace(message))
                                {
                                    node = parameter;

                                    if (message.StartsWith("    "))
                                    {
                                        message = message.Substring(4);

                                        var lastItem = parameter.FindLastChild<Item>();

                                        // only indent if it's not a "For SearchPath..." message - that one needs to be directly under parameter
                                        if (lastItem != null && !message.StartsWith("For SearchPath"))
                                        {
                                            node = lastItem;
                                        }
                                    }

                                    if (!string.IsNullOrEmpty(message))
                                    {
                                        if (message.IndexOf('=') != -1)
                                        {
                                            var kvp = Utilities.ParseNameValue(message);
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
                                bool isResult = message.StartsWith("Unified primary reference ") ||
                                    message.StartsWith("Primary reference ") ||
                                    message.StartsWith("Dependency ") ||
                                    message.StartsWith("Unified Dependency ");

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
                        if (message.StartsWith("Global Properties") ||
                            message.StartsWith("Additional Properties") ||
                            message.StartsWith("Overriding Global Properties") ||
                            message.StartsWith("Removing Properties"))
                        {
                            node.GetOrCreateNodeWithName<Folder>(message);
                            return;
                        }

                        node = node.FindLastChild<Folder>();
                        if (message[0] == ' ' && message[1] == ' ')
                        {
                            message = message.Substring(2);
                        }

                        var kvp = Utilities.ParseNameValue(message);
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

                if (message.StartsWith("Task") && message.Contains("skipped"))
                {
                    messageNode.IsLowRelevance = true;
                }
            }
            else if (args.BuildEventContext?.ProjectContextId > 0)
            {
                var project = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId);
                node = project;

                if (message.StartsWith("Target") && message.Contains("skipped"))
                {
                    var targetName = stringTable.Intern(Utilities.ParseQuotedSubstring(message));
                    if (targetName != null)
                    {
                        node = project.GetOrAddTargetByName(targetName);
                        messageNode.IsLowRelevance = true;
                    }
                }
            }
            else if (args.BuildEventContext != null && Reflector.GetEvaluationId(args.BuildEventContext) is int evaluationId && evaluationId != int.MinValue)
            {
                var evaluation = construction.EvaluationFolder;
                var project = evaluation.FindChild<Project>(p => p.Id == evaluationId);
                node = project;

                if (node != null && node.FindChild<Message>(message) != null)
                {
                    // avoid duplicate messages
                    return;
                }
            }

            if (node == null)
            {
                node = construction.Build;

                if (IsEvaluationMessage(message))
                {
                    if (!evaluationMessagesAlreadySeen.Add(message))
                    {
                        return;
                    }

                    node = construction.EvaluationFolder;
                }
                else if (message.StartsWith("The target") && message.Contains("does not exist in the project, and will be ignored"))
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
                    node = construction.Build.GetOrCreateNodeWithName<Folder>("DetailedSummary");
                }
            }

            node.AddChild(nodeToAdd);
        }

        private bool IsEvaluationMessage(string message)
        {
            return message.StartsWith("Search paths being used")
                || message.StartsWith("Overriding target")
                || message.StartsWith("Trying to import")
                || message.StartsWith("Property reassignment")
                || message.StartsWith("Importing project")
                || (message.StartsWith("Project \"") && message.Contains("was not imported by"));
        }

        /// <summary>
        /// Handler for a TaskCommandLine log event. Sets the command line arguments on the appropriate task. 
        /// </summary>
        /// <param name="args">The <see cref="TaskCommandLineEventArgs"/> instance containing the event data.</param>
        public void AddCommandLine(TaskCommandLineEventArgs args)
        {
            var project = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(args.BuildEventContext.TargetId);
            var task = target.GetTaskById(args.BuildEventContext.TaskId);

            task.CommandLineArguments = stringTable.Intern(args.CommandLine);
        }
    }
}
