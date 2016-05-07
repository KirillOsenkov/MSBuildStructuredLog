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

        /// <summary>
        /// Initializes the logger and subscribes to the relevant events.
        /// </summary>
        /// <param name="eventSource">The available events that processEvent logger can subscribe to.</param>
        public override void Initialize(IEventSource eventSource)
        {
            ProcessParameters();

            construction = new Construction();
            construction.Completed += Construction_Completed;

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
        }

        private void Construction_Completed()
        {
            XmlLogWriter.SaveToXml(construction.Build, _logFile);
        }

        /// <summary>
        /// Processes the parameters given to the logger from MSBuild.
        /// </summary>
        /// <exception cref="LoggerException">
        /// </exception>
        private void ProcessParameters()
        {
            const string invalidParamSpecificationMessage = @"Need to specify a log file using the following pattern: '/logger:StructuredLogger,StructuredLogger.dll;buildlog.xml";

            if (Parameters == null)
            {
                throw new LoggerException(invalidParamSpecificationMessage);
            }

            string[] parameters = Parameters.Split(';');

            if (parameters.Length != 1)
            {
                throw new LoggerException(invalidParamSpecificationMessage);
            }

            _logFile = parameters[0];
        }
    }
}
