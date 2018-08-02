using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class BinarySerializationTests
    {
        //[Fact]
        public void TestWriter()
        {
            var build = Serialization.Read(@"D:\XmlBuildLogs\contentsync.xml");
            Serialization.Write(build, @"D:\1.buildlog");
        }

        [Fact]
        public void SimpleBuild()
        {
            var build = new Build();
            build.Succeeded = true;
            build.AddChild(new Message() { Text = "MessageText" });
            build.AddChild(new Property() { Name = "PropertyName", Value = "PropertyValue" });
            var xmlFile1 = @"1.xml";
            var xmlFile2 = @"2.xml";
            Serialization.Write(build, xmlFile1);
            var buildLogFile = @"1.buildlog";
            Serialization.Write(build, buildLogFile);
            build = Serialization.Read(buildLogFile);
            Serialization.Write(build, xmlFile2);
            Serialization.Write(build, @"2.buildlog");
            Differ.AreDifferent(xmlFile1, xmlFile2);
        }
    }
}
