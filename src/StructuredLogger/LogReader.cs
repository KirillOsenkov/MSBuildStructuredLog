using System;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class LogReader
    {
        public static Build ReadLog(string xmlFilePath)
        {
            var doc = XDocument.Load(xmlFilePath, LoadOptions.PreserveWhitespace);
            var root = doc.Root;

            var build = new Build();
            build.Succeeded = GetBoolean(root, "BuildSucceeded");
            build.StartTime = GetDateTime(root, "StartTime");
            build.EndTime = GetDateTime(root, "EndTime");
            build.ErrorCount = GetInteger(root, "Errors");
            build.WarningCount = GetInteger(root, "Warnings");

            foreach (var element in root.Elements())
            {
                ReadElement(element, build);
            }

            return build;
        }

        private static void ReadElement(XElement element, Build build)
        {
            var name = element.Name.LocalName;
            if (name == "BuildMessageEvents")
            {
                ReadMessages(element, build);
            }
            else if (name == "Properties")
            {
                build.Properties = ReadProperties(element);
            }
            else if (name == "Project")
            {
                var project = ReadProject(element);
                build.AddProject(project);
            }
        }

        private static Project ReadProject(XElement element)
        {
            var project = new Project();
            project.Name = GetString(element, "Name");
            project.StartTime = GetDateTime(element, "StartTime");
            project.EndTime = GetDateTime(element, "EndTime");
            project.ProjectFile = GetString(element, "ProjectFile");

            foreach (var subelement in element.Elements())
            {
                ReadElement(subelement, project);
            }

            return project;
        }

        private static void ReadElement(XElement element, Project project)
        {
            var name = element.Name.LocalName;
            if (name == "ProjectMessageEvents")
            {
                ReadMessages(element, project);
            }
            else if (name == "Properties")
            {
                project.Properties = ReadProperties(element);
            }
            else if (name == "Project")
            {
                var subproject = ReadProject(element);
                project.AddChildProject(subproject);
            }
            else if (name == "Target")
            {
                var target = ReadTarget(element);
                project.AddTarget(target);
            }
        }

        private static Target ReadTarget(XElement element)
        {
            var target = new Target();

            target.Name = GetString(element, "Name");
            target.StartTime = GetDateTime(element, "StartTime");
            target.EndTime = GetDateTime(element, "EndTime");

            foreach (var subelement in element.Elements())
            {
                ReadElement(subelement, target);
            }

            return target;
        }

        private static void ReadElement(XElement element, Target target)
        {
            var name = element.Name.LocalName;
            if (name == "TargetMessages")
            {
                ReadMessages(element, target);
            }
            else if (name == "Properties")
            {
                target.Properties = ReadProperties(element);
            }
            else if (name == "ItemGroups")
            {
                ReadTaskParameters<ItemGroup>(element, target);
            }
            else if (name == "Target")
            {
                var childTarget = ReadTarget(element);
                target.AddChildTarget(childTarget);
            }
            else if (name == "Task")
            {
                var task = ReadTask(element);
                target.AddChildTask(task);
            }
        }

        private static void ReadTaskParameters<T>(XElement element, LogProcessNode node) where T : TaskParameter, new()
        {
            foreach (var subelement in element.Elements())
            {
                var taskParameter = ReadTaskParameter<T>(subelement);
                node.AddTaskParameter(taskParameter);
            }
        }

        private static T ReadTaskParameter<T>(XElement element) where T : TaskParameter, new()
        {
            var taskParameter = new T();
            taskParameter.Name = element.Name.LocalName;
            if (!element.HasElements && element.Value != null)
            {
                taskParameter.AddItem(new Item(element.Value));
                return taskParameter;
            }

            foreach (var itemElement in element.Elements())
            {
                Item item = ReadItem(itemElement, taskParameter);
                taskParameter.AddItem(item);
            }

            return taskParameter;
        }

        private static Item ReadItem(XElement itemElement, TaskParameter taskParameter)
        {
            var include = GetString(itemElement, "Include");
            var remove = GetString(itemElement, "Remove");
            if (remove != null)
            {
                taskParameter.ItemAttributeName = "Remove";
            }
            else
            {
                taskParameter.ItemAttributeName = "Include";
            }

            var itemText = include ?? remove ?? itemElement.Value;
            var result = new Item(itemText);

            ReadItemMetadata(itemElement, result);
            return result;
        }

        private static void ReadItemMetadata(XElement itemElement, Item result)
        {
            foreach (var metadataElement in itemElement.Elements())
            {
                var name = metadataElement.Name.LocalName;
                var value = metadataElement.Value;
                result.AddMetadata(name, value);
            }
        }

        private static Microsoft.Build.Logging.StructuredLogger.Task ReadTask(XElement element)
        {
            var task = new Microsoft.Build.Logging.StructuredLogger.Task();

            task.Name = GetString(element, "Name");
            task.FromAssembly = GetString(element, "FromAssembly");
            task.StartTime = GetDateTime(element, "StartTime");
            task.EndTime = GetDateTime(element, "EndTime");

            foreach (var subelement in element.Elements())
            {
                ReadElement(subelement, task);
            }

            return task;
        }

        private static void ReadElement(XElement element, Microsoft.Build.Logging.StructuredLogger.Task task)
        {
            var name = element.Name.LocalName;
            if (name == "TaskMessages")
            {
                ReadMessages(element, task);
            }
            else if (name == "Parameters")
            {
                ReadTaskParameters<InputParameter>(element, task);
            }
            else if (name == "OutputItems")
            {
                ReadTaskParameters<OutputItem>(element, task);
            }
            else if (name == "OutputProperties")
            {
                ReadTaskParameters<OutputProperty>(element, task);
            }
            else if (name == "CommandLineArguments")
            {
                ReadCommandLineArguments(element, task);
            }
        }

        private static void ReadCommandLineArguments(XElement element, Microsoft.Build.Logging.StructuredLogger.Task task)
        {
            var value = element.Value;
            task.CommandLineArguments = value;
        }

        private static PropertyBag ReadProperties(XElement element)
        {
            var propertyBag = new PropertyBag();
            foreach (var propertyElement in element.Elements())
            {
                var value = propertyElement.Value;
                var name = GetString(propertyElement, "Name");
                propertyBag.AddProperty(name, value);
            }

            return propertyBag;
        }

        private static void ReadMessages(XElement element, LogProcessNode logProcessNode)
        {
            foreach (var messageElement in element.Elements())
            {
                var message = ReadMessage(messageElement);
                logProcessNode.AddMessage(message);
            }
        }

        private static Message ReadMessage(XElement messageElement)
        {
            var text = messageElement.Value;
            var timestamp = GetDateTime(messageElement, "Timestamp");
            var message = new Message(text, timestamp);
            return message;
        }

        private static bool GetBoolean(XElement element, string attributeName)
        {
            var text = GetString(element, attributeName);
            bool result;
            bool.TryParse(text, out result);
            return result;
        }

        private static int GetInteger(XElement element, string attributeName)
        {
            var text = GetString(element, attributeName);
            int result;
            int.TryParse(text, out result);
            return result;
        }

        private static DateTime GetDateTime(XElement element, string attributeName)
        {
            var text = GetString(element, attributeName);
            DateTime result;
            DateTime.TryParse(text, out result);
            return result;
        }

        private static string GetString(XElement element, string attributeName)
        {
            return element.Attribute(attributeName)?.Value;
        }
    }
}
