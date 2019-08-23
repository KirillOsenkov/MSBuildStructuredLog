using System;

namespace Microsoft.Build.Framework
{
    public class PropertyInitialValueSetEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the UninitializedPropertyRead class.
        /// </summary>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
        public PropertyInitialValueSetEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the UninitializedPropertyRead class.
        /// </summary>
        public PropertyInitialValueSetEventArgs
        (
            string propertyName,
            string propertyValue,
            string propertySource,
            string message,
            string helpKeyword = null,
            string senderName = null,
            MessageImportance importance = MessageImportance.Low
        ) : base(message, helpKeyword, senderName, importance)
        {
            this.PropertyName = propertyName;
            this.PropertySource = propertySource;
            this.PropertyValue = propertyValue;
        }

        public string PropertyName { get; set; }

        public string PropertySource { get; set; }

        public string PropertyValue { get; set; }
    }
}
