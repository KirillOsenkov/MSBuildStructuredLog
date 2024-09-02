using System;
using System.Reflection;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class ExceptionHandler
    {
        public static void Initialize()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private static void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            ErrorReporting.ReportException(e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ErrorReporting.ReportException(e.ExceptionObject as Exception);
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

            if (ex?.InnerException != null)
            {
                return Unwrap(ex.InnerException);
            }

            return ex;
        }
    }
}
