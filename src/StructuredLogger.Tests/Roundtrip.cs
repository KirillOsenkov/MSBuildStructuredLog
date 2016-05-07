using System.Diagnostics;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class Roundtrip
    {
        // [Fact]
        public void RoundtripTest()
        {
            var file = @"D:\log1.xml";
            //var build = XmlLogReader.ReadLog(file);
            //var newName = Path.ChangeExtension(file, ".new.xml");
            //build.SaveToXml(newName);
            //Process.Start("devenv", $"/diff \"{file}\" \"{newName}\"");
        }
    }
}
