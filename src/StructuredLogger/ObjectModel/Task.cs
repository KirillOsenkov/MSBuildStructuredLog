using System;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an MSBuild task execution.
    /// </summary>
    public class Task : LogProcessNode
    {
        public string FromAssembly { get; set; }
        public string CommandLineArguments { get; set; }

        public override string ToString()
        {
            return $"Task: Id={Id} Name={Name}";
        }
    }
}
