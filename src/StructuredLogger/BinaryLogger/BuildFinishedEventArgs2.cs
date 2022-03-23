using System;
using System.Collections.Generic;

namespace Microsoft.Build.Framework
{
    public class BuildFinishedEventArgs2 : BuildFinishedEventArgs
    {
        private IDictionary<string, string> environmentVariables;

        public IDictionary<string, string> EnvironmentVariables
        {
            get
            {
                return environmentVariables;
            }
        }

        /// <summary>
        /// Constructor which allows environment variable-derived properties to be set
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="succeeded">True indicates a successful build</param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        /// <param name="environmentVariables">Properties derived from environment variables</param>
        /// <param name="messageArgs">message arguments</param>
        public BuildFinishedEventArgs2
        (
            string message,
            string helpKeyword,
            bool succeeded,
            DateTime eventTimestamp,
            IDictionary<string, string> environmentVariables,
            params object[] messageArgs
        )
            : base(message, helpKeyword, succeeded, eventTimestamp, messageArgs)
        {
            this.environmentVariables = environmentVariables;
        }
    }
}