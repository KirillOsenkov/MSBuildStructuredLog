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

        //[Fact]
        public void SimpleBuild()
        {
            var build = new Build();
            build.Succeeded = true;
            build.AddChild(new Message() { Text = "MessageText" });
            build.AddChild(new Property() { Name = "PropertyName", Value = "PropertyValue" });
            var file1 = @"D:\1.xml";
            var file2 = @"D:\2.xml";
            Serialization.Write(build, file1);
            var filePath = @"D:\1.buildlog";
            Serialization.Write(build, filePath);
            build = Serialization.Read(filePath);
            Serialization.Write(build, file2);
            Serialization.Write(build, @"D:\2.buildlog");
            Differ.AreDifferent(file1, file2);
        }
    }
}
