using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogger.Tests;
using Xunit;
using Xunit.Abstractions;
using static StructuredLogger.Tests.TestUtilities;
using BinaryLogger = Microsoft.Build.Logging.StructuredLogger.BinaryLogger;

namespace Microsoft.Build.UnitTests
{
    public class BinaryLoggerTests : IDisposable
    {
        private readonly ITestOutputHelper output;

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
               <Message Text='[$(MSBuildThisFileFullPath)]'/>
            </Target>

            <Target Name='Target2' AfterTargets='Target1'>
               <Exec Command='echo a'/>
            </Target>

            <Target Name='Target3' AfterTargets='Target2'>
               <MSBuild Projects='$(MSBuildThisFileFullPath)' Properties='GP=a' Targets='InnerTarget1'/>
               <MSBuild Projects='$(MSBuildThisFileFullPath)' Properties='GP=b' Targets='InnerTarget1'/>
               <MSBuild Projects='$(MSBuildThisFileFullPath)' Properties='GP=a' Targets='InnerTarget2'/>
               <MSBuild Projects='$(MSBuildThisFileFullPath)' Properties='GP=a' Targets='InnerTarget2'/>
            </Target>

            <Target Name='InnerTarget1'>
               <Message Text='inner target 1'/>
            </Target>

            <Target Name='InnerTarget2'>
               <Message Text='inner target 2'/>
            </Target>
         </Project>";

        public BinaryLoggerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TestBuildTreeStructureCount()
        {
            var binLog = GetTestFile("1.binlog");
            var binaryLogger = new BinaryLogger();
            binaryLogger.Parameters = binLog;
            var buildSuccessful = MSBuild.BuildProjectFromFile(s_testProject, binaryLogger);

            Assert.True(buildSuccessful);

            var build = Serialization.Read(binLog);

            var projectEvaluations = build.FindChildrenRecursive<ProjectEvaluation>();
            Assert.Equal(3, projectEvaluations.Count);
            Assert.Equal(3, new HashSet<ProjectEvaluation>(projectEvaluations).Count);

            var projectInvocations = build.FindChildrenRecursive<Project>();
            Assert.Equal(5, projectInvocations.Count);
            Assert.Equal(5, new HashSet<Project>(projectInvocations).Count);

            Assert.Equal(7, build.FindChildrenRecursive<Target>().Count);

            Assert.Equal(10, build.FindChildrenRecursive<Task>().Count);

            Assert.Equal(4, build.FindChildrenRecursive<Item>().Count);

        }

        [Theory]
        [InlineData(true)]
        [InlineData(false, Skip="https://github.com/KirillOsenkov/MSBuildStructuredLog/issues/295")]
        public void TestBinaryLoggerRoundtrip(bool useInMemoryProject)
        {
            var binLog = GetTestFile("1.binlog");
            var binaryLogger = new BinaryLogger();
            binaryLogger.Parameters = binLog;
            var buildSuccessful = useInMemoryProject
                ? MSBuild.BuildProjectInMemory(s_testProject, binaryLogger)
                : MSBuild.BuildProjectFromFile(s_testProject, binaryLogger);

            Assert.True(buildSuccessful);

            var build = Serialization.Read(binLog);
            var xml1 = GetTestFile("1.xml");
            var xml2 = GetTestFile("2.xml");

            Serialization.Write(build, xml1);

            Serialization.Write(build, GetTestFile("1.buildlog"));
            build = Serialization.Read(GetTestFile("1.buildlog"));

            Serialization.Write(build, xml2);

            Assert.False(Differ.AreDifferent(xml1, xml2));

            build = XlinqLogReader.ReadFromXml(xml1);
            Serialization.Write(build, GetTestFile("3.xml"));
            Assert.False(Differ.AreDifferent(xml1, GetTestFile("3.xml")));

            build = Serialization.Read(xml1);
            Serialization.Write(build, GetTestFile("4.xml"));

            Assert.False(Differ.AreDifferent(xml1, GetTestFile("4.xml")));
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