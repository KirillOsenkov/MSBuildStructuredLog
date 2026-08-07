using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace StructuredLogViewer.Avalonia
{
    class Program
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();

        public static int Main(string[] args)
        {
            AppDomain.MonitoringIsEnabled = true;

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    Microsoft.Build.Logging.StructuredLogger.ErrorReporting.ReportException(e.ExceptionObject as Exception);
                }
                catch
                {
                }
            };

            var app = BuildAvaloniaApp();
            int result = app.StartWithClassicDesktopLifetime(args);

            // if there's a Save As operation in progress, wait for it to finish
            var mainWindow = (app.Instance?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;
            mainWindow?.InProgressTask.Wait();

            return result;
        }
    }
}
