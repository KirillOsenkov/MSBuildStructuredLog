using System;
using System.Reflection;

namespace StructuredLogViewer
{
    public class ExceptionHandler
    {
        public static void Initialize()
        {
#if NET46
            AppDomain.CurrentDomain.UnhandledException += (o, e) => 
                Microsoft.Build.Logging.StructuredLogger.ErrorReporting.ReportException(e.ExceptionObject as Exception);
#endif
        }

        public static Exception Unwrap(Exception ex)
        {
            if (ex is ReflectionTypeLoadException reflectionTypeLoadException)
            {
                if (reflectionTypeLoadException.LoaderExceptions != null && reflectionTypeLoadException.LoaderExceptions.Length > 0)
                {
                    return Unwrap(reflectionTypeLoadException.LoaderExceptions[0]);
                }
            }

            if (ex is TargetInvocationException tie)
            {
                return Unwrap(tie.InnerException);
            }

            if (ex is AggregateException ae)
            {
                return Unwrap(ae.Flatten().InnerExceptions[0]);
            }

            if (ex.InnerException != null)
            {
                return Unwrap(ex.InnerException);
            }

            return ex;
        }
    }
}
