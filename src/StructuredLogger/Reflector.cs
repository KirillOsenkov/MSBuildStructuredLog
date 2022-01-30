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
    }
}