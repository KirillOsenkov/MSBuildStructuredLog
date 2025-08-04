using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Logging.StructuredLogger;

#nullable enable

namespace StructuredLogViewer
{
    /// <summary>
    /// Helper class for showing files and directories in the system file explorer.
    /// </summary>
    public static class FileExplorerHelper
    {
        /// <summary>
        /// Shows the specified file or directory in the system file explorer.
        /// </summary>
        /// <param name="path">The path to show in the file explorer.</param>
        public static void ShowInExplorer(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    // Show file in file manager
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        Process.Start("open", $"-R \"{path}\"");
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        // Try common Linux file managers
                        var directory = Path.GetDirectoryName(path);
                        if (Directory.Exists(directory))
                        {
                            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
                        }
                    }
                }
                else if (Directory.Exists(path))
                {
                    // Open directory
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Process.Start("explorer.exe", $"\"{path}\"");
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        Process.Start("open", $"\"{path}\"");
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    }
                }
            }
            catch
            {
                // If that fails, try just opening the directory
                try
                {
                    var directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                    if (Directory.Exists(directory))
                    {
                        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
                    }
                }
                catch
                {
                    // Ignore any errors
                }
            }
        }

        /// <summary>
        /// Gets a valid file path from the specified node if it contains one.
        /// </summary>
        /// <param name="selectedNode">The node to extract a file path from.</param>
        /// <returns>A valid file path if found, otherwise null.</returns>
        public static string? GetFilePathFromNode(BaseNode? selectedNode)
        {
            if (selectedNode == null)
            {
                return null;
            }

            // Check for NameValueNode first
            if (selectedNode is NameValueNode nameValueNode && IsValidExistingPath(nameValueNode.Value))
            {
                return nameValueNode.Value;
            }

            // Check for Item node (representing items in ItemGroups)
            if (selectedNode is Item item && IsValidExistingPath(item.Text))
            {
                return item.Text;
            }

            // Check for file path in standard nodes
            if (selectedNode is Import import && IsValidExistingPath(import.ImportedProjectFilePath))
            {
                return import.ImportedProjectFilePath;
            }

            if (selectedNode is IHasSourceFile file && IsValidExistingPath(file.SourceFilePath))
            {
                return file.SourceFilePath;
            }

            return null;
        }

        /// <summary>
        /// Checks if the specified path is a valid existing file or directory path.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>True if the path is valid and exists, otherwise false.</returns>
        public static bool IsValidExistingPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                // Check if it looks like a valid file path
                if (path!.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                {
                    return false;
                }

                // Check if the file or directory actually exists
                return File.Exists(path) || Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }
    }
}
