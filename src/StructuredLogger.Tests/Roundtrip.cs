using System.Diagnostics;
using System.IO;
using Xunit;

namespace StructuredLogger.Tests
{
    public class Roundtrip
    {
        // [Fact]
        public void RoundtripTest()
        {
            var file = @"D:\log1.xml";
            var build = LogReader.ReadLog(file);
            var newName = Path.ChangeExtension(file, ".new.xml");
            build.SaveToXml(newName);
            Process.Start("devenv", $"/diff \"{file}\" \"{newName}\"");
        }
    }
}
