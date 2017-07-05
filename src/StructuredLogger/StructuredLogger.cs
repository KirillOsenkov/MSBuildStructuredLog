using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Logger class to handle, parse, and route messages from the MSBuild logging system.
    /// </summary>
    public class StructuredLogger : Logger
    {
        private Construction construction;

        /// <summary>
        /// The path to the log file specified by the user
        /// </summary>
        private string _logFile;
        private ProjectImportsCollector projectImportsCollector;

        public static Build CurrentBuild { get; set; }
        public static bool SaveLogToDisk { get; set; } = true;

        /// <summary>
        /// Initializes the logger and subscribes to the relevant events.
        /// </summary>
        /// <param name="eventSource">The available events that processEvent logger can subscribe to.</param>
        public override void Initialize(IEventSource eventSource)
        {
            Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", "true");
            Environment.SetEnvironmentVariable("MSBUILDLOGIMPORTS", "1");

            // Set this environment variable to log AssemblyFoldersEx search results from ResolveAssemblyReference
            // Environment.SetEnvironmentVariable("MSBUILDLOGVERBOSERARSEARCHRESULTS", "true");

            ProcessParameters();

            if (SaveLogToDisk)
            {
                try
                {
                    projectImportsCollector = new ProjectImportsCollector(_logFile);
                }
                catch (Exception ex)
                {
                    throw new LoggerException($"Failed to create the source archive for log file {_logFile}", ex);
                }
            }

            construction = new Construction();

            eventSource.BuildStarted += construction.BuildStarted;
            eventSource.BuildFinished += construction.BuildFinished;
            eventSource.ProjectStarted += construction.ProjectStarted;
            eventSource.ProjectFinished += construction.ProjectFinished;
            eventSource.TargetStarted += construction.TargetStarted;
            eventSource.TargetFinished += construction.TargetFinished;
            eventSource.TaskStarted += construction.TaskStarted;
            eventSource.TaskFinished += construction.TaskFinished;
            eventSource.MessageRaised += construction.MessageRaised;
            eventSource.WarningRaised += construction.WarningRaised;
            eventSource.ErrorRaised += construction.ErrorRaised;
            eventSource.CustomEventRaised += construction.CustomEventRaised;
            eventSource.StatusEventRaised += construction.StatusEventRaised;

            if (projectImportsCollector != null)
            {
                eventSource.AnyEventRaised += EventSource_AnyEventRaised;
            }

            projectImportedEventArgsType = typeof(BuildEventArgs)
                .GetTypeInfo()
                .Assembly
                .GetType("Microsoft.Build.Framework.ProjectImportedEventArgs");
            if (projectImportedEventArgsType != null)
            {
                importedProjectFile = projectImportedEventArgsType.GetProperty("ImportedProjectFile", BindingFlags.Public | BindingFlags.Instance);
                unexpandedProject = projectImportedEventArgsType.GetProperty("UnexpandedProject", BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private Type projectImportedEventArgsType;
        private PropertyInfo importedProjectFile;
        private PropertyInfo unexpandedProject;

        private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
        {
            try
            {
                if (projectImportedEventArgsType != null && e.GetType() == projectImportedEventArgsType)
                {
                    string importedProjectFile = (string)this.importedProjectFile.GetValue(e);
                    //string unexpandedProject = (string)this.unexpandedProject.GetValue(e);
                    //var buildMessage = (BuildMessageEventArgs)e;
                    //ProjectImportedEventArgs args = new ProjectImportedEventArgs(buildMessage.LineNumber, buildMessage.ColumnNumber, buildMessage.Message);
                    //args.ImportedProjectFile = importedProjectFile;
                    //args.UnexpandedProject = unexpandedProject;
                    //args.BuildEventContext = buildMessage.BuildEventContext;
                    projectImportsCollector.AddFile(importedProjectFile);
                    return;
                }

                projectImportsCollector?.IncludeSourceFiles(e);
            }
            catch
            {
            }
        }

        public override void Shutdown()
        {
            base.Shutdown();

            if (projectImportsCollector != null)
            {
                var archiveFilePath = projectImportsCollector.ArchiveFilePath;

                projectImportsCollector.Close();
                projectImportsCollector = null;

                if (File.Exists(archiveFilePath))
                {
                    var bytes = File.ReadAllBytes(archiveFilePath);
                    construction.Build.SourceFilesArchive = bytes;
                    File.Delete(archiveFilePath);
                }
            }

            if (SaveLogToDisk)
            {
                try
                {
                    if (Path.IsPathRooted(_logFile))
                    {
                        var parentDirectory = Path.GetDirectoryName(_logFile);
                        if (!Directory.Exists(parentDirectory))
                        {
                            Directory.CreateDirectory(parentDirectory);
                        }
                    }

                    Serialization.Write(construction.Build, _logFile);
                }
                catch (Exception ex)
                {
                    ErrorReporting.ReportException(ex);
                }
            }
            else
            {
                CurrentBuild = construction.Build;
            }
        }

        /// <summary>
        /// Processes the parameters given to the logger from MSBuild.
        /// </summary>
        /// <exception cref="LoggerException">
        /// </exception>
        private void ProcessParameters()
        {
            const string invalidParamSpecificationMessage = @"Need to specify a log file using the following pattern: '/logger:StructuredLogger,StructuredLogger.dll;log.buildlog";

            if (Parameters == null)
            {
                throw new LoggerException(invalidParamSpecificationMessage);
            }

            string[] parameters = Parameters.Split(';');

            if (parameters.Length != 1)
            {
                throw new LoggerException(invalidParamSpecificationMessage);
            }

            _logFile = parameters[0].TrimStart('"').TrimEnd('"');
        }
    }
}
