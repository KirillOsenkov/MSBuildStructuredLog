using System;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogger.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    public class BinaryLoggerTests : IDisposable
    {
        private static string s_testProject = @"
         <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
            <PropertyGroup>
               <TestProperty>Test</TestProperty>
            </PropertyGroup>
            <ItemGroup>
               <TestItem Include=""Test"" />
            </ItemGroup>
            <Target Name='Target1'>
               <Message Text='MessageOutputText'/>
            </Target>
            <Target Name='Target2' AfterTargets='Target1'>
               <Exec Command='echo a'/>
            </Target>
         </Project>";

        public BinaryLoggerTests(ITestOutputHelper output)
        {
        }

        [Fact]
        public void TestBinaryLoggerRoundtrip()
        {
            var binLog = "1.binlog";
            var binaryLogger = new BinaryLogger();
            binaryLogger.Parameters = binLog;
            MSBuild.BuildProject(s_testProject, binaryLogger);

            var build = Serialization.Read(binLog);
            Assert.Equal("", GetProperty(build));
            Serialization.Write(build, "1.xml");

            Serialization.Write(build, "1.buildlog");
            build = Serialization.Read("1.buildlog");
            Assert.Equal("", GetProperty(build));
            Serialization.Write(build, "2.xml");

            Assert.False(Differ.AreDifferent("1.xml", "2.xml"));

            build = XlinqLogReader.ReadFromXml("1.xml");
            Assert.Equal("", GetProperty(build));
            Serialization.Write(build, "3.xml");
            Assert.False(Differ.AreDifferent("1.xml", "3.xml"));

            build = Serialization.Read("1.xml");
            Assert.Equal("", GetProperty(build));
            Serialization.Write(build, "4.xml");

            Assert.False(Differ.AreDifferent("1.xml", "4.xml"));
        }

        private static string GetProperty(Logging.StructuredLogger.Build build)
        {
            var property = build.FindFirstDescendant<Project>().FindChild<Folder>("Properties").FindChild<Property>(p => p.Name == "FrameworkSDKRoot").Value;
            return property;
        }

        public void Dispose()
        {
        }
    }
}