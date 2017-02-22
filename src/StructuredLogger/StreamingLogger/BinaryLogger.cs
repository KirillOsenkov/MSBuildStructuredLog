using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.Serialization;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BinaryLogger : Logger
    {
        public const int FileFormatVersion = 1;

        private Stream stream;
        private BetterBinaryWriter binaryWriter;
        private EventArgsWriter eventArgsWriter;

        public string FilePath { get; set; }

        public override void Initialize(IEventSource eventSource)
        {
            Environment.SetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING", "true");
            Verbosity = LoggerVerbosity.Diagnostic;

            ProcessParameters();

            try
            {
                stream = new FileStream(FilePath, FileMode.Create);
            }
            catch (Exception e)
            {
                throw new LoggerException("Invalid file logger file path: " + FilePath, e);
            }

            stream = new GZipStream(stream, CompressionLevel.Optimal);
            binaryWriter = new BetterBinaryWriter(stream);
            eventArgsWriter = new EventArgsWriter(binaryWriter);

            binaryWriter.Write(FileFormatVersion);

            eventSource.AnyEventRaised += EventSource_AnyEventRaised;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            if (stream != null)
            {
                // It's hard to determine whether we're at the end of decoding GZipStream
                // so add an explicit 0 at the end to signify end of file
                stream.WriteByte(0);
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
                lock (eventArgsWriter)
                {
                    eventArgsWriter.Write(e);
                }
            }

            // A way to call into built-in serialization using reflection. Not used here.
            // writeToStream = (Action<BuildEventArgs, BinaryWriter>)Delegate.CreateDelegate(
            //    typeof(Action<BuildEventArgs, BinaryWriter>),
            //    typeof(BuildEventArgs).GetMethod("WriteToStream", BindingFlags.Instance | BindingFlags.NonPublic));
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
