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
    }
}
