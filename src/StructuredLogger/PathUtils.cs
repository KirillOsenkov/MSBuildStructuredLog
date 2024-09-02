using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public static class PathUtils
    {
        public static readonly string RootPath = GetRootPath();
        public static readonly string TempPath = Path.Combine(RootPath, "Temp");

        private static string GetRootPath()
        {
#if NETCORE
            var path = Path.GetTempPath();
#else
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#endif

            path = Path.Combine(path, "Microsoft", "MSBuildStructuredLog");
            return path;
        }

        internal const string ExtendedPathPrefix = @"\\?\";
        internal const string UncPathPrefix = @"\\";
        internal const string UncExtendedPrefixToInsert = @"?\UNC\";
        internal const string UncExtendedPathPrefix = @"\\?\UNC\";
        internal const string DevicePathPrefix = @"\\.\";
        internal const int DevicePrefixLength = 4;
        internal const int MaxShortPath = 260;
        internal const int MaxShortDirectoryPath = 248;

        internal static bool IsExtended(string path)
        {
            // While paths like "//?/C:/" will work, they're treated the same as "\\.\" paths.
            // Skipping of normalization will *only* occur if back slashes ('\') are used.
            return path.Length >= DevicePrefixLength
                && path[0] == '\\'
                && (path[1] == '\\' || path[1] == '?')
                && path[2] == '?'
                && path[3] == '\\';
        }

        public static bool HasInvalidVolumeSeparator(string path)
        {
            // Toss out paths with colons that aren't a valid drive specifier.
            // Cannot start with a colon and can only be of the form "C:" or "\\?\C:".
            // (Note that we used to explicitly check "http:" and "file:"- these are caught by this check now.)

            // We don't care about skipping starting space for extended paths. Assume no knowledge of extended paths if we're forcing old path behavior.
            bool isExtended = IsExtended(path);
            int startIndex = isExtended ? ExtendedPathPrefix.Length : PathStartSkip(path);

            // If we start with a colon
            if ((path.Length > startIndex && path[startIndex] == Path.VolumeSeparatorChar)
                // Or have an invalid drive letter and colon
                || (path.Length >= startIndex + 2 && path[startIndex + 1] == Path.VolumeSeparatorChar && !IsValidDriveChar(path[startIndex]))
                // Or have any colons beyond the drive colon
                || (path.Length > startIndex + 2 && path.IndexOf(Path.VolumeSeparatorChar, startIndex + 2) != -1))
            {
                return true;
            }

            return false;
        }

        internal static bool IsDirectorySeparator(char c)
        {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }

        internal static bool IsValidDriveChar(char value)
        {
            return ((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z'));
        }

        internal static int PathStartSkip(string path)
        {
            int startIndex = 0;
            while (startIndex < path.Length && path[startIndex] == ' ')
            {
                startIndex++;
            }

            if (startIndex > 0 && (startIndex < path.Length && IsDirectorySeparator(path[startIndex]))
                || (startIndex + 1 < path.Length && path[startIndex + 1] == Path.VolumeSeparatorChar && IsValidDriveChar(path[startIndex])))
            {
                // Go ahead and skip spaces as we're either " C:" or " \"
                return startIndex;
            }

            return 0;
        }
    }
}
