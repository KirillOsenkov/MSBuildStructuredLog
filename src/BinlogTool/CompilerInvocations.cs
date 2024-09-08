using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class CompilerInvocations : BinlogToolCommandBase
    {
        public static void Run(string binLogFilePath, string outputFilePath)
        {
            var invocations = CompilerInvocationsReader.ReadInvocations(binLogFilePath);
        }
    }
}
