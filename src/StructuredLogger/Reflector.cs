using System;
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

        private static PropertyInfo lazyFormattedBuildEventArgs_RawArguments;
        public static PropertyInfo LazyFormattedBuildEventArgs_RawArguments
        {
            get
            {
                if (lazyFormattedBuildEventArgs_RawArguments == null)
                {
                    lazyFormattedBuildEventArgs_RawArguments =
                        typeof(LazyFormattedBuildEventArgs).GetProperty("RawArguments", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return lazyFormattedBuildEventArgs_RawArguments;
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

        private static FieldInfo buildMessageEventArgs_lineNumber;
        public static FieldInfo BuildMessageEventArgs_lineNumber
        {
            get
            {
                if (buildMessageEventArgs_lineNumber == null)
                {
                    buildMessageEventArgs_lineNumber = typeof(BuildMessageEventArgs).GetField("lineNumber", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return buildMessageEventArgs_lineNumber;
            }
        }

        private static FieldInfo buildMessageEventArgs_columnNumber;
        public static FieldInfo BuildMessageEventArgs_columnNumber
        {
            get
            {
                if (buildMessageEventArgs_columnNumber == null)
                {
                    buildMessageEventArgs_columnNumber = typeof(BuildMessageEventArgs).GetField("columnNumber", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return buildMessageEventArgs_columnNumber;
            }
        }

        public static object[] GetArguments(LazyFormattedBuildEventArgs args)
        {
            if (LazyFormattedBuildEventArgs_RawArguments != null)
            {
                return LazyFormattedBuildEventArgs_RawArguments.GetValue(args) as object[];
            }
            else if (LazyFormattedBuildEventArgs_arguments != null)
            {
                return LazyFormattedBuildEventArgs_arguments.GetValue(args) as object[];
            }

            return Array.Empty<object>();
        }

        private static MethodInfo enumerateItemsPerType;
        public static MethodInfo GetEnumerateItemsPerTypeMethod(Type itemDictionary)
        {
            if (enumerateItemsPerType == null)
            {
                enumerateItemsPerType = itemDictionary.GetMethod("EnumerateItemsPerType", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            return enumerateItemsPerType;
        }
    }
}