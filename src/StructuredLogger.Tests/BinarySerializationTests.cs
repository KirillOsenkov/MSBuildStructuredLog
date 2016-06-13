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

        //[Fact]
        public void SimpleBuild()
        {
            var build = new Build();
            build.Succeeded = true;
            build.AddChild(new Message() { Text = "MessageText" });
            build.AddChild(new Property() { Name = "PropertyName", Value = "PropertyValue" });
            var file1 = @"D:\1.xml";
            var file2 = @"D:\2.xml";
            XmlLogWriter.WriteToXml(build, file1);
            var filePath = @"D:\1.buildlog";
            BinaryLogWriter.Write(build, filePath);
            build = BinaryLogReader.Read(filePath);
            XmlLogWriter.WriteToXml(build, file2);
            BinaryLogWriter.Write(build, @"D:\2.buildlog");
            Differ.AreDifferent(file1, file2);
        }
    }
}
