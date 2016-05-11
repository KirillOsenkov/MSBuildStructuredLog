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

        public MessageProcessor(Construction construction)
        {
            this.construction = construction;
        }

        public void Process(BuildMessageEventArgs args)
        {
            // Task Input / Outputs
            if (args.Message.StartsWith(TaskParameterMessagePrefix))
            {
                var task = GetTask(args);
                var folder = task.GetOrCreateNodeWithName<Folder>("Parameters");
                var parameter = ItemGroupParser.ParsePropertyOrItemList(args.Message, TaskParameterMessagePrefix);
                folder.AddChild(parameter);
            }
            else if (args.Message.StartsWith(OutputItemsMessagePrefix))
            {
                var task = GetTask(args);
                var folder = task.GetOrCreateNodeWithName<Folder>("OutputItems");
                var parameter = ItemGroupParser.ParsePropertyOrItemList(args.Message, OutputItemsMessagePrefix);
                folder.AddChild(parameter);
            }
            else if (args.Message.StartsWith(OutputPropertyMessagePrefix))
            {
                var task = GetTask(args);
                var folder = task.GetOrCreateNodeWithName<Folder>("OutputProperties");
                var parameter = ItemGroupParser.ParsePropertyOrItemList(args.Message, OutputPropertyMessagePrefix);
                folder.AddChild(parameter);
            }

            // Item / Property groups
            else if (args.Message.StartsWith(PropertyGroupMessagePrefix))
            {
                AddPropertyGroup(args, PropertyGroupMessagePrefix);
            }
            else if (args.Message.StartsWith(ItemGroupIncludeMessagePrefix))
            {
                AddItemGroup(args, ItemGroupIncludeMessagePrefix, new AddItem());
            }
            else if (args.Message.StartsWith(ItemGroupRemoveMessagePrefix))
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
                var match = Regex.Match(args.Message, taskAssemblyPattern);
                if (match.Success)
                {
                    construction.SetTaskAssembly(match.Groups["task"].Value, match.Groups["assembly"].Value);
                }
                else
                {
                    // Just the generic log message or something we currently don't handle in the object model.
                    AddMessage(args, args.Message);
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

            target.AddChild(new Property { Name = name, Value = value });
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
            var itemGroup = ItemGroupParser.ParsePropertyOrItemList(args.Message, prefix);
            var property = itemGroup as Property;
            if (property != null)
            {
                itemGroup = new Item { Name = property.Name, Text = property.Value };
                containerNode.Name = property.Name;
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
            var messageNode = new Message { Text = message, Timestamp = args.Timestamp };

            if (args.BuildEventContext.TaskId > 0)
            {
                node = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId)
                    .GetTargetById(args.BuildEventContext.TargetId)
                    .GetTaskById(args.BuildEventContext.TaskId);
                var task = node as Task;
                if (task != null && task.Name == "ResolveAssemblyReference")
                {
                    node = task.GetOrCreateNodeWithName<Folder>("Inputs");
                    if (message.StartsWith("    "))
                    {
                        var parameter = node.FindLastChild<Parameter>();
                        if (parameter != null)
                        {
                            message = message.Substring(4);
                            if (!string.IsNullOrWhiteSpace(message))
                            {
                                parameter.AddChild(new Item() { Text = message });
                            }

                            return;
                        }
                    }
                    else
                    {
                        node = node.GetOrCreateNodeWithName<Parameter>(message.TrimEnd(':'));
                        return;
                    }
                }
            }
            else if (args.BuildEventContext.TargetId > 0)
            {
                node = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId)
                    .GetTargetById(args.BuildEventContext.TargetId);

                if (message.StartsWith("Task") && message.Contains("skipped"))
                {
                    messageNode.IsLowRelevance = true;
                }
            }
            else if (args.BuildEventContext.ProjectContextId > 0)
            {
                var project = construction.GetOrAddProject(args.BuildEventContext.ProjectContextId);
                node = project;

                if (message.StartsWith("Target") && message.Contains("skipped"))
                {
                    var targetName = ParseTargetName(message);
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
                else if (args.BuildEventContext.NodeId == 0 &&
                         args.BuildEventContext.ProjectContextId == 0 &&
                         args.BuildEventContext.ProjectInstanceId == 0 &&
                         args.BuildEventContext.TargetId == 0 &&
                         args.BuildEventContext.TaskId == 0)
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

            task.CommandLineArguments = args.CommandLine;
        }
    }
}
