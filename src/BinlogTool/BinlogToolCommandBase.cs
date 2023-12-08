using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public abstract class BinlogToolCommandBase
    {
        public ForwardCompatibilityReadingHandler CompatibilityHandler { protected get; init; }

        protected Build ReadBuild(string binLogFilePath, bool throwOnPathNotFound = true)
        {
            if (string.IsNullOrEmpty(binLogFilePath) || !File.Exists(binLogFilePath))
            {
                if(throwOnPathNotFound)
                {
                    throw new FileNotFoundException("Specified binlog was not found.", binLogFilePath);
                }

                return null;
            }

            return CompatibilityHandler?.ReadBuild(binLogFilePath) ?? BinaryLog.ReadBuild(binLogFilePath);
        }
    }
}
