using System;
using System.Windows;

namespace StructuredLogViewer
{
    public class Entrypoint
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var app = new Application();
            var window = new MainWindow();
            app.Run(window);
        }
    }
}
