using System;
using System.Diagnostics;
using System.IO;
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
#if LOCAL_DIAGNOSTICS
            LocalControlServer server = null;
            string sessionPath = GetArgumentValue(args, "--local-control");
#endif
            var window = new MainWindow();
#if LOCAL_DIAGNOSTICS
            if (!string.IsNullOrWhiteSpace(sessionPath))
            {
                server = new LocalControlServer(app, sessionPath);
                server.Attach(window);
            }
#endif
            app.Run(window);
#if LOCAL_DIAGNOSTICS
            server?.Dispose();
#endif

            // wait for potential background operations to finish before shutting down
            window.InProgressTask.Wait();
        }

#if LOCAL_DIAGNOSTICS
        private static string GetArgumentValue(string[] args, string name)
        {
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(args[i + 1]);
                }
            }

            return null;
        }
#endif

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ErrorReporting.ReportException(e.Exception);
            DialogService.ShowMessageBox(
                    "Unexpected exception. Sorry about that.\r\nPlease Ctrl+C to copy this text and file an issue at https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/new\r\n\r\n" + e.Exception.ToString());
            e.Handled = true;
        }
    }
}
