using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class SaveStrings
    {
        public void Run(string binLogFilePath, string outputFilePath)
        {
            var build = BinaryLog.ReadBuild(binLogFilePath);
            var strings = build.StringTable.Instances.OrderBy(s => s).ToArray();

            Serialization.WriteStringsToFile(outputFilePath, strings);
        }
    }
}