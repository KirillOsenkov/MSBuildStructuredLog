using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BinaryLogger : Logger
    {
        private Stream stream;
        private BetterBinaryWriter binaryWriter;
        private Action<BuildEventArgs, BinaryWriter> writeToStream;

        public string FilePath { get; set; }

        public override void Initialize(IEventSource eventSource)
        {
            Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", "true");

            ProcessParameters();

            try
            {
                stream = new FileStream(FilePath, FileMode.Create);
            }
            catch (Exception e)
            {
                throw new LoggerException("Invalid file logger file path: " + FilePath, e);
            }

            //stream = new GZipStream(stream, CompressionLevel.Optimal);

            binaryWriter = new BetterBinaryWriter(stream);

            writeToStream = (Action<BuildEventArgs, BinaryWriter>)Delegate.CreateDelegate(
                typeof(Action<BuildEventArgs, BinaryWriter>),
                typeof(BuildEventArgs).GetMethod("WriteToStream", BindingFlags.Instance | BindingFlags.NonPublic));
            eventSource.AnyEventRaised += EventSource_AnyEventRaised;
            eventSource.CustomEventRaised += EventSource_CustomEventRaised;
            eventSource.BuildStarted += EventSource_BuildStarted;
            eventSource.BuildFinished += EventSource_BuildFinished;
            eventSource.ProjectStarted += EventSource_ProjectStarted;
            eventSource.ProjectFinished += EventSource_ProjectFinished;
            eventSource.TargetStarted += EventSource_TargetStarted;
            eventSource.TargetFinished += EventSource_TargetFinished;
            eventSource.TaskStarted += EventSource_TaskStarted;
            eventSource.TaskFinished += EventSource_TaskFinished;
            eventSource.ErrorRaised += EventSource_ErrorRaised;
            eventSource.WarningRaised += EventSource_WarningRaised;
            eventSource.MessageRaised += EventSource_MessageRaised;
            eventSource.StatusEventRaised += EventSource_StatusEventRaised;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            if (stream != null)
            {
                stream.Flush();
                stream.Close();
                stream = null;
            }
        }

        public void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
        {
            Write(e);
        }

        private void Write(BuildEventArgs e)
        {
            if (stream != null)
            {
                binaryWriter.Write(e.GetType().MetadataToken);
                writeToStream(e, binaryWriter);
            }
        }

        private void EventSource_StatusEventRaised(object sender, BuildStatusEventArgs e)
        {
        }

        private void EventSource_CustomEventRaised(object sender, CustomBuildEventArgs e)
        {
        }

        private void EventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
        }

        private void EventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
        }

        private void EventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
        }

        private void EventSource_TaskFinished(object sender, TaskFinishedEventArgs e)
        {
        }

        private void EventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
        }

        private void EventSource_TargetFinished(object sender, TargetFinishedEventArgs e)
        {
        }

        private void EventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
        }

        private void EventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
        }

        private void EventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        {
        }

        private void EventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
        }

        private void EventSource_BuildStarted(object sender, BuildStartedEventArgs e)
        {
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

            FilePath = parameters[0].TrimStart('"').TrimEnd('"');
        }
    }
}
