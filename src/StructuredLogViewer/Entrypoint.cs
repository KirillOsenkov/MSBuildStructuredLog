using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class Entrypoint
    {
        [STAThread]
        public static void Main(string[] args)
        {
            ExceptionHandler.Initialize();
            DialogService.ShowMessageBoxEvent += message => MessageBox.Show(message);
            ClipboardService.Set += Clipboard.SetText;

            AppDomain.MonitoringIsEnabled = true;

            var app = new Application();
            app.DispatcherUnhandledException += OnDispatcherUnhandledException;
            var window = new MainWindow();
            app.Run(window);

            // wait for potential background operations to finish before shutting down
            window.InProgressTask.Wait();
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ErrorReporting.ReportException(e.Exception);
            DialogService.ShowMessageBox(
                    "Unexpected exception. Sorry about that.\r\nPlease Ctrl+C to copy this text and file an issue at https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/new\r\n\r\n" + e.Exception.ToString());
            e.Handled = true;
        }
    }
}
