using System;
using System.IO;

namespace StructuredLogViewer
{
    public class ExceptionHandler
    {
        public static void Initialize()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private static readonly string logFilePath = Path.Combine(SettingsService.GetRootPath(), "Log.txt");

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject;
                if (ex != null)
                {
                    File.WriteAllText(logFilePath, ex.ToString());
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
