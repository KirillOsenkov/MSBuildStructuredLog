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
        public Construction Construction => construction;

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
        }

        private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
        {
            try
            {
                if (e is ProjectImportedEventArgs projectImportedEventArgs)
                {
                    string importedProjectFile = projectImportedEventArgs.ImportedProjectFile;
                    projectImportsCollector.AddFile(importedProjectFile);
                    return;
                }
                else if (e is ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
                {
                    string projectFile = projectEvaluationFinishedEventArgs.ProjectFile;
                    object profilerResult = projectEvaluationFinishedEventArgs.ProfilerResult;
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
