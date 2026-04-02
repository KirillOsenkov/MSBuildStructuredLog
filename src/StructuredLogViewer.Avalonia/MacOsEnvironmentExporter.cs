using System;
using System.Diagnostics;

namespace StructuredLogViewer.Avalonia;

public static class MacOsEnvironmentExporter
{
    /// <summary>
    /// Provides a workaround for the restricted environment that macOS imposes on processes launched
    /// via LaunchServices (e.g., by double-clicking an <c>.app</c> bundle in Finder or the Dock).
    /// See https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/941 for details.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is relevant exclusively on <b>macOS</b>, and only when the application is packaged
    /// and distributed as an application bundle (<c>myApp.app</c>).
    /// </para>
    /// <para>
    /// When an app bundle is launched through LaunchServices (by the double-click in the finder or by the
    /// `open -a myApp.app` command and as opposed to being started from a terminal), the process inherits
    /// a minimal, stripped-down environment. In particular,
    /// the <c>PATH</c> variable is severely limited and does not include directories that are
    /// normally configured by the user's shell profile (e.g., <c>~/.zshrc</c>, <c>~/.bash_profile</c>).
    /// As a result, the application cannot locate and execute tools such as <c>dotnet build</c> or
    /// other CLI utilities that rely on a properly configured <c>PATH</c>.
    /// </para>
    /// <para>
    /// This class works around the restriction by spawning the user's default login shell as an
    /// interactive session, capturing the <c>PATH</c> it produces after loading all profile scripts,
    /// and merging that value into the environment of the current process.
    /// </para>
    /// <para>
    /// When the application is run directly from a terminal (detected via the presence of the
    /// <c>TERM</c> environment variable), the fix is intentionally skipped because the shell
    /// environment is already fully set up.
    /// </para>
    /// </remarks>
    public static void InheritUserPath()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            // Check if we're running as the app bundle
            var executablePath = Environment.ProcessPath ?? string.Empty;
            var insideBundleStructure = executablePath.Contains(".app/Contents/MacOS", StringComparison.OrdinalIgnoreCase);

            // If the TERM environment variable exists, we are running inside a terminal session.
            // If it is null, we were launched by the macOS GUI (LaunchServices).
            var hasTerminalEnvironment = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM"));

            // If we're not running inside the bundle structure - exit
            // If we're running inside the bundle structure, but we're run via a terminal (and not by a launchd) - exit
            if (!insideBundleStructure || hasTerminalEnvironment)
            {
                return;
            }

            // Get the user's default shell (defaults to zsh on modern macOS)
            var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/zsh";

            // Spawn the shell as an interactive login shell to force it to load .zshrc/.bash_profile
            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = "-ilc \"echo $PATH\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return;
            }

            // Read the outputted PATH
            // Synchronous read seems safe here as we only redirect single standard stream (stdout), so no deadlock risk here.
            var userPath = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (string.IsNullOrEmpty(userPath))
            {
                return;
            }

            // Update the environment variable for the current C# process
            var existingPath = Environment.GetEnvironmentVariable("PATH");
            var combinedPath = string.IsNullOrEmpty(existingPath) ? userPath : $"{userPath}:{existingPath}";
            Environment.SetEnvironmentVariable("PATH", combinedPath);
        }
        catch
        {
            //ignore
        }
    }
}
