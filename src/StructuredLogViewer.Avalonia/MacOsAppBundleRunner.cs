using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Avalonia;

/// <summary>
/// Native macOS instance runner for opening new independent instances of the current AppBundle.
/// </summary>
public static class MacOsAppBundleRunner
{
    /// <summary>
    /// Opens a new independent instance of the application. macOS only.
    /// </summary>
    public static readonly ICommand NewInstanceCommand = new Command(RunNewInstance);

    /// <summary>
    /// Opens a new independent instance of the application from the specified app bundle path. macOS only.
    /// </summary>
    /// <param name="appBundlePath">Full path to the <c>.app</c> bundle to launch.</param>
    public static void RunAppBundle(string appBundlePath)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(appBundlePath);

        if (!appBundlePath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("App bundle path must point to a '.app' bundle.", nameof(appBundlePath));
        }

        if (!Directory.Exists(appBundlePath))
        {
            throw new ArgumentException($"App bundle does not exist or is not a directory: '{appBundlePath}'.", nameof(appBundlePath));
        }

        try
        {
            // When running as a .app bundle use `open -n <bundle>` so that macOS
            // properly activates a new instance with a clean environment.
            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("-n");
            psi.ArgumentList.Add(appBundlePath);
            Process.Start(psi);
        }
        catch
        {
            // Ignore failures – launching a new instance is best-effort.
        }
    }

    private static void RunNewInstance()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var processPath = Environment.ProcessPath;
        if (processPath is null)
        {
            return;
        }

        var appBundlePath = GetMacOsAppBundlePath(processPath);
        if (appBundlePath is null)
        {
            // Not running inside an .app bundle – launch as a plain executable.
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = processPath,
                    UseShellExecute = false,
                });
            }
            catch
            {
                // Ignore failures – launching a new instance is best-effort.
            }

            return;
        }

        RunAppBundle(appBundlePath);
    }


    /// <summary>
    /// If <paramref name="executablePath"/> lives inside a macOS .app bundle
    /// (i.e. …/Foo.app/Contents/MacOS/Foo) returns the bundle root path; otherwise null.
    /// </summary>
    private static string? GetMacOsAppBundlePath(string executablePath)
    {
        const string contentsMarker = ".app/Contents/MacOS/";
        var idx = executablePath.IndexOf(contentsMarker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var dir = Path.GetDirectoryName(executablePath);
        if (dir == null)
        {
            return null;
        }
        var macOsDir = new DirectoryInfo(dir);
        return macOsDir.Parent?.Parent?.FullName;
    }
}
