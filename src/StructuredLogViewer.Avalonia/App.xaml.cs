using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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
            {
                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;

                if (this.TryGetFeature<IActivatableLifetime>() is { } activatableLifetime)
                {
                    activatableLifetime.Activated += (sender, e) =>
                    {
                        if (e is FileActivatedEventArgs { Files.Count: > 0 } files &&
                            files.Files[0].TryGetLocalPath() is { } path)
                        {
                            mainWindow.OpenActivatedFile(path);
                        }
                    };
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
