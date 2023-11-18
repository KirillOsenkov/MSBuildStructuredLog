using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace StructuredLogger.Tests
{
    public class CompilerInvocationTests
    {
        [Theory]
        [InlineData(
            @"C:\Program Files\dotnet\dotnet.exe exec ""C:\Program Files\dotnet\sdk\5.0.100-rc.2.20479.15\Roslyn\bincore\csc.dll"" /noconfig", "/noconfig")]
        [InlineData(@"foo\csc.exe a.cs /out:a.dll", "a.cs /out:a.dll")]
        public void Parse1(string arg, string expected)
        {
            var result = CompilerInvocationsReader.TrimCompilerExeFromCommandLine(arg, CompilerInvocation.CSharp);
            Assert.Equal(expected, result);
        }

        //[Fact]
        internal void ReadRecordsTest()
        {
            var binlog = @"C:\temp\msbuild.binlog";
            var records = BinaryLog.ReadRecords(binlog);
            string lastTask = null;

            foreach (var record in records)
            {
                if (record.Args is TaskStartedEventArgs taskStarted)
                {
                    lastTask = taskStarted.TaskName;
                }
                else if (record.Args is TaskParameterEventArgs taskParameters)
                {
                    if (lastTask == "Csc" && taskParameters.ItemType == "Compile")
                    {
                        foreach (ITaskItem item in taskParameters.Items)
                        {
                            var csFilePath = item.ItemSpec;
                        }
                    }
                }
            }
        }
    }
}