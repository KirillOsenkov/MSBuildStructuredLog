using System;
using System.IO;
using System.Reflection;
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
            var binLog = GetFullPath("1.binlog");
            var binaryLogger = new BinaryLogger();
            binaryLogger.Parameters = binLog;
            MSBuild.BuildProject(s_testProject, binaryLogger);

            var build = Serialization.Read(binLog);
            var xml1 = GetFullPath("1.xml");
            Serialization.Write(build, xml1);

            Serialization.Write(build, GetFullPath("1.buildlog"));
            build = Serialization.Read(GetFullPath("1.buildlog"));
            Serialization.Write(build, GetFullPath("2.xml"));

            Assert.False(Differ.AreDifferent(xml1, GetFullPath("2.xml")));

            build = XlinqLogReader.ReadFromXml(xml1);
            Serialization.Write(build, GetFullPath("3.xml"));
            Assert.False(Differ.AreDifferent(xml1, GetFullPath("3.xml")));

            build = Serialization.Read(xml1);
            Serialization.Write(build, GetFullPath("4.xml"));

            Assert.False(Differ.AreDifferent(xml1, GetFullPath("4.xml")));
        }

        private static string GetFullPath(string fileName)
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName);
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