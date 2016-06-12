using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class BinarySerializationTests
    {
        //[Fact]
        public void TestWriter()
        {
            var build = XmlLogReader.ReadFromXml(@"D:\XmlBuildLogs\contentsync.xml");
            BinaryLogWriter.Write(build, @"D:\1.buildlog");
        }
    }
}
