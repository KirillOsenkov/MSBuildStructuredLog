using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace StructuredLogger.BinaryLogger
{
    /// <summary>
    /// Base class for all build check event args.
    /// Not intended to be extended by external code.
    /// </summary>
    internal abstract class BuildCheckEventArgs : BuildEventArgs
    { }

    /// <summary>
    /// Transport mean for the BuildCheck tracing data from additional nodes.
    /// </summary>
    /// <param name="tracingData"></param>
    internal sealed class BuildCheckTracingEventArgs(Dictionary<string, TimeSpan> tracingData) : BuildCheckEventArgs
    {
        public Dictionary<string, TimeSpan> TracingData { get; private set; } = tracingData;
    }

    internal sealed class BuildCheckAcquisitionEventArgs(string acquisitionPath, string projectPath) : BuildCheckEventArgs
    {
        public string AcquisitionPath { get; private set; } = acquisitionPath;

        public string ProjectPath { get; private set; } = projectPath;
    }

    internal sealed class BuildCheckResultMessage : BuildMessageEventArgs
    {
        public BuildCheckResultMessage(string message) => RawMessage = message;
    }
}
