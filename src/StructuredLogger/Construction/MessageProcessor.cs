using System;
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

        public void Process(BuildMessageEventArgs args)
        {
            if (args == null)
            {
                return;
            }

            var message = args.Message;
            if (message == null)
            {
                return;
            }

            // Task Input / Outputs
            if (message.StartsWith(TaskParameterMessagePrefix))
            {
                var task = GetTask(args);
                var folder = task.GetOrCreateNodeWithName<Folder>("Parameters");
                var parameter = ItemGroupParser.ParsePropertyOrItemList(message, TaskParameterMessagePrefix, stringTable);
                folder.AddChild(parameter);
            }
            else if (message.StartsWith(OutputItemsMessagePrefix))
            {
                var task = GetTask(args);
                var folder = task.GetOrCreateNodeWithName<Folder>("OutputItems");
                var parameter = ItemGroupParser.ParsePropertyOrItemList(message, OutputItemsMessagePrefix, stringTable);
                folder.AddChild(parameter);
            }
            else if (message.StartsWith(OutputPropertyMessagePrefix))
            {
                var task = GetTask(args);
                var folder = task.GetOrCreateNodeWithName<Folder>("OutputProperties");
                var parameter = ItemGroupParser.ParsePropertyOrItemList(message, OutputPropertyMessagePrefix, stringTable);
                folder.AddChild(parameter);
            }

            // Item / Property groups
            else if (message.StartsWith(PropertyGroupMessagePrefix))
            {
                AddPropertyGroup(args, PropertyGroupMessagePrefix);
            }
            else if (message.StartsWith(ItemGroupIncludeMessagePrefix))
            {
                AddItemGroup(args, ItemGroupIncludeMessagePrefix, new AddItem());
            }
            else if (message.StartsWith(ItemGroupRemoveMessagePrefix))
            {
                AddItemGroup(args, ItemGroupRemoveMessagePrefix, new RemoveItem());
            }
            else
            {
                // This was command line arguments for task
                var taskArgs = args as TaskCommandLineEventArgs;
                if (taskArgs != null)
                {
                    AddCommandLine(taskArgs);
                    return;
                }

                // A task from assembly message (parses out the task name and assembly path).
                const string taskAssemblyPattern = "Using \"(?<task>.+)\" task from (assembly|the task factory) \"(?<assembly>.+)\"\\.";
                var match = Regex.Match(message, taskAssemblyPattern);
                if (match.Success)
                {
                    construction.SetTaskAssembly(
                        stringTable.Intern(match.Groups["task"].Value),
                        stringTable.Intern(match.Groups["assembly"].Value));
                }
                else
                {
                    // Just the generic log message or something we currently don't handle in the object model.
                    AddMessage(args, message);
                }
            }
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

            var equals = message.IndexOf('=');
            var name = message.Substring(0, equals);
            var value = message.Substring(equals + 1);

            target.AddChild(new Property
            {
                Name = stringTable.Intern(name),
                Value = stringTable.Intern(value)
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

        /// <summary>
        /// Handles a generic BuildMessage event and assigns it to the appropriate logging node.
        /// </summary>
        /// <param name="args">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        public void AddMessage(LazyFormattedBuildEventArgs args, string message)
        {
            TreeNode node = null;
            var messageNode = new Message
            {
                Text = stringTable.Intern(message),
                Timestamp = args.Timestamp
            };

            if (args.BuildEventContext?.TaskId > 0)
            {
                node = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId)
                    .GetTargetById(args.BuildEventContext.TargetId)
                    .GetTaskById(args.BuildEventContext.TaskId);
                var task = node as Task;
                if (task != null && task.Name == "ResolveAssemblyReference")
                {
                    Folder inputs = task.GetOrCreateNodeWithName<Folder>("Inputs");
                    Folder results = task.FindChild<Folder>(c => c.Name == "Results");
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
                                    if (lastItem != null)
                                    {
                                        node = lastItem;
                                    }
                                }

                                if (!string.IsNullOrEmpty(message))
                                {
                                    node.AddChild(new Item()
                                    {
                                        Text = stringTable.Intern(message)
                                    });
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

                        node.GetOrCreateNodeWithName<Parameter>(message.TrimEnd(':'));
                        return;
                    }
                }
            }
            else if (args.BuildEventContext?.TargetId > 0)
            {
                node = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId)
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
                    var targetName = stringTable.Intern(ParseTargetName(message));
                    if (targetName != null)
                    {
                        node = project.GetOrAddTargetByName(targetName);
                        messageNode.IsLowRelevance = true;
                    }
                }
            }

            if (node == null)
            {
                node = construction.Build;

                if (message.StartsWith("Overriding target"))
                {
                    var folder = construction.Build.GetOrCreateNodeWithName<Folder>("TargetOverrides");
                    folder.IsLowRelevance = true;
                    node = folder;
                    messageNode.IsLowRelevance = true;
                }
                else if (message.StartsWith("The target") && message.Contains("does not exist in the project, and will be ignored"))
                {
                    var folder = construction.Build.GetOrCreateNodeWithName<Folder>("MissingTargets");
                    folder.IsLowRelevance = true;
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

            node.AddChild(messageNode);
        }

        private string ParseTargetName(string message)
        {
            int firstQuote = message.IndexOf('"');
            if (firstQuote == -1)
            {
                return null;
            }

            int secondQuote = message.IndexOf('"', firstQuote + 1);
            if (secondQuote == -1)
            {
                return null;
            }

            if (secondQuote - firstQuote < 2)
            {
                return null;
            }

            return message.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
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
