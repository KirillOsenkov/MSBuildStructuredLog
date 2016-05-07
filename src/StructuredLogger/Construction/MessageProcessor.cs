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
                AddItemGroup(args, ItemGroupIncludeMessagePrefix, new AddItemGroup());
            }
            else if (args.Message.StartsWith(ItemGroupRemoveMessagePrefix))
            {
                AddItemGroup(args, ItemGroupRemoveMessagePrefix, new RemoveItemGroup());
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

        private Task GetTask(BuildMessageEventArgs buildMessageEventArgs)
        {
            var project = construction.GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(buildMessageEventArgs.BuildEventContext.TargetId);
            var task = target.GetTaskById(buildMessageEventArgs.BuildEventContext.TaskId);
            return task;
        }

        /// <summary>
        /// Handles BuildMessage event when a property discovery/evaluation is logged.
        /// </summary>
        /// <param name="buildMessageEventArgs">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        /// <param name="prefix">The prefix string.</param>
        public void AddPropertyGroup(BuildMessageEventArgs buildMessageEventArgs, string prefix)
        {
            string message = buildMessageEventArgs.Message.Substring(prefix.Length);

            var project = construction.GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(buildMessageEventArgs.BuildEventContext.TargetId);

            var equals = message.IndexOf('=');
            var name = message.Substring(0, equals);
            var value = message.Substring(equals + 1);

            target.AddChild(new Property { Name = name, Value = value });
        }

        /// <summary>
        /// Handles BuildMessage event when an ItemGroup discovery/evaluation is logged.
        /// </summary>
        /// <param name="buildMessageEventArgs">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        /// <param name="prefix">The prefix string.</param>
        public void AddItemGroup(BuildMessageEventArgs buildMessageEventArgs, string prefix, LogProcessNode containerNode)
        {
            var project = construction.GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(buildMessageEventArgs.BuildEventContext.TargetId);
            var itemGroup = ItemGroupParser.ParsePropertyOrItemList(buildMessageEventArgs.Message, prefix);
            containerNode.AddChild(itemGroup);
            target.AddChild(containerNode);
        }

        /// <summary>
        /// Handles a generic BuildMessage event and assigns it to the appropriate logging node.
        /// </summary>
        /// <param name="buildMessageEventArgs">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        public void AddMessage(LazyFormattedBuildEventArgs buildMessageEventArgs, string message)
        {
            LogProcessNode node = null;

            if (buildMessageEventArgs.BuildEventContext.TaskId > 0)
            {
                node = construction.GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId)
                    .GetTargetById(buildMessageEventArgs.BuildEventContext.TargetId)
                    .GetTaskById(buildMessageEventArgs.BuildEventContext.TaskId);
            }
            else if (buildMessageEventArgs.BuildEventContext.TargetId > 0)
            {
                node = construction.GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId)
                    .GetTargetById(buildMessageEventArgs.BuildEventContext.TargetId);
            }
            else if (buildMessageEventArgs.BuildEventContext.ProjectContextId > 0)
            {
                node = construction.GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId);
            }

            if (node == null)
            {
                node = construction.Build;
            }

            var messages = node.GetOrCreateNodeWithName<Folder>("Messages");
            messages.AddChild(new Message { Text = message, Timestamp = buildMessageEventArgs.Timestamp });
        }

        /// <summary>
        /// Handler for a TaskCommandLine log event. Sets the command line arguments on the appropriate task. 
        /// </summary>
        /// <param name="taskCommandLineEventArgs">The <see cref="TaskCommandLineEventArgs"/> instance containing the event data.</param>
        public void AddCommandLine(TaskCommandLineEventArgs taskCommandLineEventArgs)
        {
            var project = construction.GetOrAddProject(taskCommandLineEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(taskCommandLineEventArgs.BuildEventContext.TargetId);
            var task = target.GetTaskById(taskCommandLineEventArgs.BuildEventContext.TaskId);

            task.CommandLineArguments = taskCommandLineEventArgs.CommandLine;
        }
    }
}
