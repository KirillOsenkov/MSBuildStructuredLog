using Avalonia;

namespace StructuredLogViewer.Avalonia
{
    class Program
    {
        static void Main(string[] args)
        {
            ExceptionHandler.Initialize();
            //DialogService.ShowMessageBoxEvent += message => MessageBox.Show(message);
            ClipboardService.Set += text => Application.Current.Clipboard.SetTextAsync(text);

            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .Start<MainWindow>();
        }
    }
}
