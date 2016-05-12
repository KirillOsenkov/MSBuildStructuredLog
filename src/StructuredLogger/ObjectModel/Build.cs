using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an MSBuild overall build execution.
    /// </summary>
    public class Build : TimedNode
    {
        public bool IsAnalyzed { get; set; }
        public bool Succeeded { get; set; }

        public override string ToString() => "Build " + (Succeeded ? "succeeded" : "failed");
    }
}
