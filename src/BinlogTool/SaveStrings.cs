using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class SaveStrings : BinlogToolCommandBase
    {
        public void Run(string binLogFilePath, string outputFilePath)
        {
            var build = this.ReadBuild(binLogFilePath);
            var strings = build.StringTable.Instances.OrderBy(s => s).ToArray();

            Serialization.WriteStringsToFile(outputFilePath, strings);
        }
    }
}
