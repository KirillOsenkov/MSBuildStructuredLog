using Microsoft.Build.Logging.StructuredLogger;
using System;

namespace Microsoft.Build.Framework
{
    public class UninitializedPropertyReadEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the UninitializedPropertyRead class.
        /// </summary>
#if FEATURE_BINARY_SERIALIZATION
        [Serializable]
#endif
        public UninitializedPropertyReadEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the UninitializedPropertyRead class.
        /// </summary>
        public UninitializedPropertyReadEventArgs
        (
            string propertyName,
            string message,
            string helpKeyword=null,
            string senderName=null,
            MessageImportance importance = MessageImportance.Low,
            params object[] messageArgs
        ) : base(message, helpKeyword, senderName, importance)
        {
            this.PropertyName = propertyName;
        }

        public string PropertyName { get; set; }
    }
}
