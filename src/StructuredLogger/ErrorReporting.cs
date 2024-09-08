﻿namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ErrorReporting
    {
        private static readonly string logFilePath = Path.Combine(PathUtils.RootPath, "LoggerExceptions.txt");

        public static string LogFilePath => logFilePath;

        public static void ReportException(Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));

                // if the log has gotten too big, delete it
                if (File.Exists(logFilePath) && new FileInfo(logFilePath).Length > 10000000)
                {
                    File.Delete(logFilePath);
                }

                File.AppendAllText(logFilePath, ex.ToString() + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }
    }
}
