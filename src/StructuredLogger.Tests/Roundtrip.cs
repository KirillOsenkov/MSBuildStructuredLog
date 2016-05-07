using System.Diagnostics;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class Roundtrip
    {
        //[Fact]
        public void RoundtripTest()
        {
            var file = @"D:\1.xml";
            var build = XmlLogReader.ReadFromXml(file);
            var newName = Path.ChangeExtension(file, ".new.xml");
            XmlLogWriter.WriteToXml(build, newName);
            Process.Start("devenv", $"/diff \"{file}\" \"{newName}\"");
        }
    }
}
