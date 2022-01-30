using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Reflector
    {
        private static FieldInfo buildEventArgs_message;
        public static FieldInfo BuildEventArgs_message
        {
            get
            {
                if (buildEventArgs_message == null)
                {
                    buildEventArgs_message = typeof(BuildEventArgs).GetField("message", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return buildEventArgs_message;
            }
        }

        private static FieldInfo lazyFormattedBuildEventArgs_arguments;
        public static FieldInfo LazyFormattedBuildEventArgs_arguments
        {
            get
            {
                if (lazyFormattedBuildEventArgs_arguments == null)
                {
                    lazyFormattedBuildEventArgs_arguments =
                        typeof(LazyFormattedBuildEventArgs).GetField("arguments", BindingFlags.Instance | BindingFlags.NonPublic) ??
                        typeof(LazyFormattedBuildEventArgs).GetField("argumentsOrFormattedMessage", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return lazyFormattedBuildEventArgs_arguments;
            }
        }

        private static FieldInfo buildEventArgs_senderName;
        public static FieldInfo BuildEventArgs_senderName
        {
            get
            {
                if (buildEventArgs_senderName == null)
                {
                    buildEventArgs_senderName = typeof(BuildEventArgs).GetField("senderName", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return buildEventArgs_senderName;
            }
        }

        private static FieldInfo buildEventArgs_timestamp;
        public static FieldInfo BuildEventArgs_timestamp
        {
            get
            {
                if (buildEventArgs_timestamp == null)
                {
                    buildEventArgs_timestamp = typeof(BuildEventArgs).GetField("timestamp", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return buildEventArgs_timestamp;
            }
        }

        private static FieldInfo buildEventArgs_lineNumber;
        public static FieldInfo BuildEventArgs_lineNumber
        {
            get
            {
                if (buildEventArgs_lineNumber == null)
                {
                    buildEventArgs_lineNumber = typeof(BuildMessageEventArgs).GetField("lineNumber", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return buildEventArgs_lineNumber;
            }
        }

        private static FieldInfo buildEventArgs_columnNumber;
        public static FieldInfo BuildEventArgs_columnNumber
        {
            get
            {
                if (buildEventArgs_columnNumber == null)
                {
                    buildEventArgs_columnNumber = typeof(BuildMessageEventArgs).GetField("columnNumber", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return buildEventArgs_columnNumber;
            }
        }
    }
}