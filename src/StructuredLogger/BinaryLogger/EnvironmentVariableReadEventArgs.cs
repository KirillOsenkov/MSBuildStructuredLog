using System;

namespace Microsoft.Build.Framework
{
    public class EnvironmentVariableReadEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the EnvironmentVariableReadEventArgs class.
        /// </summary>
#if FEATURE_BINARY_SERIALIZATION
        [Serializable]
#endif
        public EnvironmentVariableReadEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the EnvironmentVariableReadEventArgs class.
        /// </summary>
        public EnvironmentVariableReadEventArgs
        (
            string environmentVariableName,
            string message,
            string helpKeyword=null,
            string senderName=null,
            MessageImportance importance = MessageImportance.Low,
            params object[] messageArgs
        )
            : base(null, null, null, 0, 0, 0, 0, message, helpKeyword, senderName, importance, DateTime.UtcNow, messageArgs)
        {
            EnvironmentVariableName = environmentVariableName;
        }
        

        public string EnvironmentVariableName { get; set; }
    }
}
