using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace StructuredLogViewer.Avalonia
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            UpdateTheme();
            MacOsEnvironmentExporter.InheritUserPath();
#if DEBUG
            this.AttachDeveloperTools();
#endif
        }

        public static void UpdateTheme()
        {
            Current.RequestedThemeVariant = SettingsService.UseDarkTheme
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();

            base.OnFrameworkInitializationCompleted();
        }
    }
}
