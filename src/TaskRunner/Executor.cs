using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;

namespace TaskRunner
{
    public class Executor
    {
        public static void Execute(Task task)
        {
            var assemblyFilePath = task.FromAssembly;
            if (string.IsNullOrEmpty(assemblyFilePath))
            {
                return;
            }

            Assembly assembly = null;

            if (File.Exists(assemblyFilePath))
            {
                assembly = Assembly.LoadFrom(assemblyFilePath);
            }
            else
            {
                assembly = Assembly.Load(assemblyFilePath);
            }

            var type = assembly.GetTypes().FirstOrDefault(t => t.Name == task.Name || t.FullName == task.Name);
            if (type == null)
            {
                throw new ArgumentException($"Type {task.Name} not found in assembly {assembly}");
            }

            // need to set current directory before we activate the task instance
            var project = task.GetNearestParent<Project>();
            var projectDirectory = project.ProjectDirectory;
            if (Directory.Exists(projectDirectory))
            {
                Environment.CurrentDirectory = projectDirectory;
            }

            var instance = (ITask)Activator.CreateInstance(type);
            PopulateParameters(task, instance);

            instance.BuildEngine = new BuildEngine();

            instance.Execute();
        }

        private static void PopulateParameters(Task task, object instance)
        {
            var parametersNode = task.FindChild<Folder>(Strings.Parameters);
            if (parametersNode == null)
            {
                throw new ArgumentException("Parameters node not found under task " + task.Name);
            }

            foreach (var parameter in parametersNode.Children)
            {
                try
                {
                    SetParameter(parameter, instance);
                }
                catch (MissingPropertyException exception)
                {
                    exception.MSBuildVersion = task.GetNearestParent<Build>()?.MSBuildVersion;
                    throw;
                }
            }
        }

        private static readonly char[] semicolon = { ';' };

        private static void SetParameter(object propertyOrParameter, object instance)
        {
            if (propertyOrParameter is Property property)
            {
                var propertyInfo = FindPropertyInfo(instance, property.Name, out var flags, out var type);
                var stringValue = property.Value;

                object value = stringValue;
                if (propertyInfo.PropertyType == typeof(bool))
                {
                    value = Convert.ToBoolean(stringValue);
                }
                else if (propertyInfo.PropertyType == typeof(string[]))
                {
                    if (string.IsNullOrEmpty(stringValue))
                    {
                        value = Array.Empty<string>();
                    }
                    else
                    {
                        value = stringValue.Split(semicolon, StringSplitOptions.RemoveEmptyEntries);
                    }
                }
                else if (propertyInfo.PropertyType == typeof(ITaskItem))
                {
                    value = new TaskItem(stringValue);
                }
                else if (propertyInfo.PropertyType == typeof(ITaskItem[]))
                {
                    if (string.IsNullOrEmpty(stringValue))
                    {
                        value = Array.Empty<ITaskItem>();
                    }
                    else
                    {
                        value = stringValue
                            .Split(semicolon, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => new TaskItem(s))
                            .ToArray();
                    }
                }
                else if (propertyInfo.PropertyType == typeof(int))
                {
                    if (int.TryParse(stringValue, out int intValue))
                    {
                        value = intValue;
                    }
                }

                SetPropertyValue(instance, property.Name, value);
            }
            else if (propertyOrParameter is Parameter parameter)
            {
                var taskItems = GetTaskItems(parameter);

                var propertyInfo = FindPropertyInfo(instance, parameter.Name, out var flags, out var type);
                object value = null;
                if (propertyInfo.PropertyType == typeof(string))
                {
                    string separator = ";";
                    if (propertyInfo.Name == "SolutionConfigurationContents")
                    {
                        separator = Environment.NewLine;
                    }

                    value = string.Join(separator, taskItems.Select(t => t.ItemSpec));
                }
                else if (propertyInfo.PropertyType == typeof(string[]))
                {
                    value = taskItems.Select(t => t.ItemSpec).ToArray();
                }
                else if (propertyInfo.PropertyType == typeof(ITaskItem))
                {
                    var item = taskItems.First();
                    foreach (var pair in taskItems.Skip(1).Select(e => e.ItemSpec.TrimStart().Split('=')))
                    {
                        item.SetMetadata(pair[0], pair[1]);
                    }

                    value = item;
                }
                else
                {
                    value = taskItems;
                }

                SetPropertyValue(instance, propertyInfo.Name, value);
            }
            else
            {
                throw new NotSupportedException(propertyOrParameter.ToString());
            }
        }

        private static ITaskItem[] GetTaskItems(Parameter parameter)
        {
            var list = new List<ITaskItem>();
            foreach (var item in parameter.Children)
            {
                var taskItem = GetTaskItem(item);
                list.Add(taskItem);
            }

            return list.ToArray();
        }

        private static TaskItem GetTaskItem(object itemOrProperty)
        {
            if (itemOrProperty is Item item)
            {
                var taskItem = new TaskItem(item.Text);
                foreach (var metadata in item.Children.OfType<Metadata>())
                {
                    taskItem.SetMetadata(metadata.Name, metadata.Value);
                }

                return taskItem;
            }
            else if (itemOrProperty is Property property)
            {
                var taskItem = new TaskItem(property.Name + "=" + property.Value);
                return taskItem;
            }

            throw new InvalidOperationException("Unsupported " + itemOrProperty.ToString());
        }

        public static void SetPropertyValue<T>(object instance, string propertyName, T value)
        {
            var propertyInfo = FindPropertyInfo(instance, propertyName, out var flags, out var type);

            // Workaround for Reflection bug 791391
            if (propertyInfo.DeclaringType != type)
            {
                type = propertyInfo.DeclaringType;
                propertyInfo = type.GetProperty(propertyName, flags);
            }

            propertyInfo.SetValue(instance, value, flags, null, null, null);
        }

        private static PropertyInfo FindPropertyInfo(object instance, string propertyName, out BindingFlags flags, out Type type)
        {
            flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            type = instance.GetType();
            var propertyInfo = type.GetProperty(propertyName, flags);
            if (propertyInfo == null)
            {
                throw new MissingPropertyException(className: type.FullName, propertyName: propertyName);
            }
            return propertyInfo;
        }
    }
}
