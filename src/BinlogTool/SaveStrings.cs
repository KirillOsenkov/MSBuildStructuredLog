using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class SaveStrings : BinlogToolCommandBase
    {
        public static void Run(string binLogFilePath, string outputFilePath)
        {
            var build = ReadBuild(binLogFilePath);
            var strings = build.StringTable.Instances.OrderBy(s => s).ToArray();

            Serialization.WriteStringsToFile(outputFilePath, strings);
        }
    }
}
