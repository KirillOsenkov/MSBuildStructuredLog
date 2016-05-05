using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Newtonsoft.Json;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an MSBuild overall build execution.
    /// </summary>
    public class Build : LogProcessNode
    {
        /// <summary>
        /// A lookup table mapping of project identifiers to project nodes (which can be nested multiple layers). 
        /// </summary>
        private readonly ConcurrentDictionary<int, Project> _projectIdToProjectMap = new ConcurrentDictionary<int, Project>();

        /// <summary>
        /// A mapping of task names to assembly locations.
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _taskToAssemblyMap = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool Succeeded { get; set; }

        public Build()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Build"/> class.
        /// </summary>
        /// <param name="buildStartedEventArgs">The <see cref="BuildStartedEventArgs"/> instance containing the event data.</param>
        public Build(BuildStartedEventArgs buildStartedEventArgs)
        {
            StartTime = buildStartedEventArgs.Timestamp;
            Properties = new PropertyBag(buildStartedEventArgs.BuildEnvironment);
        }

        /// <summary>
        /// Completes the build and writes to the XML log file.
        /// </summary>
        /// <param name="buildFinishedEventArgs">The <see cref="BuildFinishedEventArgs"/> instance containing the event data.</param>
        /// <param name="logFile">The XML log file.</param>
        public void CompleteBuild(BuildFinishedEventArgs buildFinishedEventArgs, string logFile, int errorCount, int warningCount, bool saveWhenFinished = true)
        {
            EndTime = buildFinishedEventArgs.Timestamp;
            Succeeded = buildFinishedEventArgs.Succeeded;
            ErrorCount = errorCount;
            WarningCount = warningCount;

            if (saveWhenFinished)
            {
                //SaveToXml(logFile);
                SaveToJson(logFile);
            }
        }

        public void SaveToJson(string logFile)
        {
            var text = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(logFile, text);
        }

        public void SaveToXml(string logFile)
        {
            var document = new XDocument();
            var root = new XElement("Build");

            document.Add(root);
            SaveToElement(root);

            document.Save(logFile);
        }

        /// <summary>
        /// Writes the build and its children to XML XElement representation.
        /// </summary>
        /// <param name="parentElement">The parent element.</param>
        public override void SaveToElement(XElement parentElement)
        {
            parentElement.Add(new XAttribute("BuildSucceeded", Succeeded));
            parentElement.Add(new XAttribute("StartTime", StartTime));
            parentElement.Add(new XAttribute("EndTime", EndTime));
            parentElement.Add(new XAttribute("Errors", ErrorCount));
            parentElement.Add(new XAttribute("Warnings", WarningCount));

            WriteChildren<Message>(parentElement, () => new XElement("BuildMessageEvents"));
            WriteProperties(parentElement);
            WriteChildren<Project>(parentElement);
        }

        /// <summary>
        /// Handler for a TargetStarted log event. Adds the target to the object structure.
        /// </summary>
        /// <param name="targetStartedEventArgs">The <see cref="TargetStartedEventArgs"/> instance containing the event data.</param>
        public void AddTarget(TargetStartedEventArgs targetStartedEventArgs)
        {
            var project = GetOrAddProject(targetStartedEventArgs.BuildEventContext.ProjectContextId);
            project.AddTarget(targetStartedEventArgs);
        }

        /// <summary>
        /// Handler for a BuildMessage log event. Adds the node to the appropriate target.
        /// </summary>
        /// <param name="buildMessageEventArgs">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        /// <param name="messagePrefix">The message prefix.</param>
        public void AddTaskParameter(BuildMessageEventArgs buildMessageEventArgs, string messagePrefix)
        {
            var project = GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(buildMessageEventArgs.BuildEventContext.TargetId);
            var task = target.GetTaskById(buildMessageEventArgs.BuildEventContext.TaskId);

            task.AddParameter(TaskParameter.Create(buildMessageEventArgs.Message, messagePrefix));
        }

        /// <summary>
        /// Handler for a TaskCommandLine log event. Sets the command line arguments on the appropriate task. 
        /// </summary>
        /// <param name="taskCommandLineEventArgs">The <see cref="TaskCommandLineEventArgs"/> instance containing the event data.</param>
        public void AddCommandLine(TaskCommandLineEventArgs taskCommandLineEventArgs)
        {
            var project = GetOrAddProject(taskCommandLineEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(taskCommandLineEventArgs.BuildEventContext.TargetId);
            var task = target.GetTaskById(taskCommandLineEventArgs.BuildEventContext.TaskId);

            task.CommandLineArguments = taskCommandLineEventArgs.CommandLine;
        }

        /// <summary>
        /// Handles BuildMessage event when a property discovery/evaluation is logged.
        /// </summary>
        /// <param name="buildMessageEventArgs">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        /// <param name="prefix">The prefix string.</param>
        public void AddPropertyGroup(BuildMessageEventArgs buildMessageEventArgs, string prefix)
        {
            string message = buildMessageEventArgs.Message.Substring(prefix.Length);

            var project = GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(buildMessageEventArgs.BuildEventContext.TargetId);

            var equals = message.IndexOf('=');
            var name = message.Substring(0, equals);
            var value = message.Substring(equals + 1);

            target.AddProperty(name, value);
        }

        /// <summary>
        /// Handles BuildMessage event when an ItemGroup discovery/evaluation is logged.
        /// </summary>
        /// <param name="buildMessageEventArgs">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        /// <param name="prefix">The prefix string.</param>
        public void AddItemGroup(BuildMessageEventArgs buildMessageEventArgs, string prefix)
        {
            var project = GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(buildMessageEventArgs.BuildEventContext.TargetId);

            target.AddTaskParameter((ItemGroup)TaskParameter.Create(buildMessageEventArgs.Message, prefix));
        }

        /// <summary>
        /// Handles a generic BuildMessage event and assigns it to the appropriate logging node.
        /// </summary>
        /// <param name="buildMessageEventArgs">The <see cref="BuildMessageEventArgs"/> instance containing the event data.</param>
        public void AddMessage(LazyFormattedBuildEventArgs buildMessageEventArgs, string message)
        {
            LogProcessNode node = this;

            if (buildMessageEventArgs.BuildEventContext.TaskId > 0)
            {
                node = GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId)
                    .GetTargetById(buildMessageEventArgs.BuildEventContext.TargetId)
                    .GetTaskById(buildMessageEventArgs.BuildEventContext.TaskId);
            }
            else if (buildMessageEventArgs.BuildEventContext.TargetId > 0)
            {
                node = GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId)
                    .GetTargetById(buildMessageEventArgs.BuildEventContext.TargetId);
            }
            else if (buildMessageEventArgs.BuildEventContext.ProjectContextId > 0)
            {
                node = GetOrAddProject(buildMessageEventArgs.BuildEventContext.ProjectContextId);
            }

            node.AddMessage(new Message(message, buildMessageEventArgs.Timestamp));
        }

        /// <summary>
        /// Handles a TaskStarted event from the log. Creates the task and assigns to the appropriate target.
        /// </summary>
        /// <param name="taskStartedEventArgs">The <see cref="TaskStartedEventArgs"/> instance containing the event data.</param>
        public void AddTask(TaskStartedEventArgs taskStartedEventArgs)
        {
            var project = GetOrAddProject(taskStartedEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(taskStartedEventArgs.BuildEventContext.TargetId);

            target.AddChildTask(new Task(taskStartedEventArgs.TaskName, taskStartedEventArgs, GetTaskAssembly((taskStartedEventArgs.TaskName))));
        }

        public void AddProject(Project project)
        {
            AddChildNode(project);
        }

        /// <summary>
        /// Handles a ProjectStarted event from the log. Creates the project and assigns it to the correct parent project (if any).
        /// </summary>
        /// <param name="projectStartedEventArgs">The <see cref="ProjectStartedEventArgs"/> instance containing the event data.</param>
        public void AddProject(ProjectStartedEventArgs projectStartedEventArgs)
        {
            Project parent = null;

            if (projectStartedEventArgs.ParentProjectBuildEventContext != null && projectStartedEventArgs.ParentProjectBuildEventContext.ProjectContextId >= 0)
            {
                parent = GetOrAddProject(projectStartedEventArgs.ParentProjectBuildEventContext.ProjectContextId);
            }

            var project = GetOrAddProject(projectStartedEventArgs.BuildEventContext.ProjectContextId, projectStartedEventArgs, parent);

            if (parent != null)
            {
                parent.AddChildProject(project);
            }
            else
            {
                // This is a "Root" project (no parent project).
                AddChildNode(project);
            }
        }

        /// <summary>
        /// Gets a project instance for the given identifier. Will create if it doesn't exist.
        /// </summary>
        /// <remarks>If the ProjectStartedEventArgs is not known at this time (null), a stub project is created.</remarks>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="projectStartedEventArgs">The <see cref="ProjectStartedEventArgs"/> instance containing the event data.</param>
        /// <param name="parentProject">The parent project, if any.</param>
        /// <returns>Project object</returns>
        public Project GetOrAddProject(int projectId, ProjectStartedEventArgs projectStartedEventArgs = null, Project parentProject = null)
        {
            Project result = _projectIdToProjectMap.GetOrAdd(projectId,
                id =>
                    new Project(id, projectStartedEventArgs,
                        parentProject == null ? Properties : parentProject.Properties));

            if (projectStartedEventArgs != null)
            {
                result.TryUpdate(projectStartedEventArgs);
            }

            return result;
        }

        /// <summary>
        /// Completes the target.
        /// </summary>
        /// <param name="targetFinishedEventArgs">The <see cref="TargetFinishedEventArgs" /> instance containing the event data.</param>
        public void CompleteTarget(TargetFinishedEventArgs targetFinishedEventArgs)
        {
            var project = GetOrAddProject(targetFinishedEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(targetFinishedEventArgs.BuildEventContext.TargetId);

            target.EndTime = targetFinishedEventArgs.Timestamp;
        }

        /// <summary>
        /// Completes the project.
        /// </summary>
        /// <param name="projectFinishedEventArgs">The <see cref="ProjectFinishedEventArgs"/> instance containing the event data.</param>
        public void CompleteProject(ProjectFinishedEventArgs projectFinishedEventArgs)
        {
            var project = GetOrAddProject(projectFinishedEventArgs.BuildEventContext.ProjectContextId);
            project.EndTime = projectFinishedEventArgs.Timestamp;
        }

        /// <summary>
        /// Completes the task.
        /// </summary>
        /// <param name="taskFinishedEventArgs">The <see cref="TaskFinishedEventArgs"/> instance containing the event data.</param>
        public void CompleteTask(TaskFinishedEventArgs taskFinishedEventArgs)
        {
            var project = GetOrAddProject(taskFinishedEventArgs.BuildEventContext.ProjectContextId);
            var target = project.GetTargetById(taskFinishedEventArgs.BuildEventContext.TargetId);
            var task = target.GetTaskById(taskFinishedEventArgs.BuildEventContext.TaskId);

            task.EndTime = taskFinishedEventArgs.Timestamp;
        }

        /// <summary>
        /// Sets the assembly location for a given task.
        /// </summary>
        /// <param name="taskName">Name of the task.</param>
        /// <param name="assembly">The assembly location.</param>
        public void SetTaskAssembly(string taskName, string assembly)
        {
            _taskToAssemblyMap.GetOrAdd(taskName, t => assembly);
        }

        /// <summary>
        /// Gets the task assembly.
        /// </summary>
        /// <param name="taskName">Name of the task.</param>
        /// <returns>The assembly location for the task.</returns>
        public string GetTaskAssembly(string taskName)
        {
            string assembly;
            return _taskToAssemblyMap.TryGetValue(taskName, out assembly) ? assembly : string.Empty;
        }

        private IEnumerable<Project> projectsSortedTopologically;
        public IEnumerable<Project> ProjectsSortedTopologically => projectsSortedTopologically ?? (projectsSortedTopologically = GetProjectsSortedTopologically());

        public IEnumerable<Project> GetProjectsSortedTopologically()
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<Project>();
            foreach (var project in this.GetChildrenOfType<Project>())
            {
                Visit(project, list, visited);
            }

            return list;
        }

        private void Visit(Project project, List<Project> list, HashSet<string> visited)
        {
            if (visited.Add(project.ProjectFile))
            {
                foreach (var childProject in project.GetChildrenOfType<Project>())
                {
                    Visit(childProject, list, visited);
                }

                list.Add(project);
            }
        }
    }
}
