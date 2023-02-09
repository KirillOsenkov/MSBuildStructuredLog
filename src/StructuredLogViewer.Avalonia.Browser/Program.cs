using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using StructuredLogViewer.Avalonia;

[assembly: SupportedOSPlatform("browser")]

internal partial class Program
{
    private static void Main(string[] args) => BuildAvaloniaApp()
        .SetupBrowserApp("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
