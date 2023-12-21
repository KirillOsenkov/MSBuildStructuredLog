using System.IO;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public abstract class BinlogToolCommandBase
    {
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

            return BinaryLog.ReadBuild(binLogFilePath);
        }
    }
}
