using System;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class ExceptionHandler
    {
        public static void Initialize()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ErrorReporting.ReportException(e.ExceptionObject as Exception);
        }
    }
}
