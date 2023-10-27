﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Build.Logging;
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
            BuildAnalyzer.AnalyzeBuild(build);

            var projectEvaluations = build.FindChildrenRecursive<ProjectEvaluation>();
            Assert.Equal(3, projectEvaluations.Count);
            Assert.Equal(3, new HashSet<ProjectEvaluation>(projectEvaluations).Count);

            var projectInvocations = build.FindChildrenRecursive<Project>();
            Assert.Equal(5, projectInvocations.Count);
            Assert.Equal(5, new HashSet<Project>(projectInvocations).Count);

            Assert.Equal(7, build.FindChildrenRecursive<Target>().Count);

            Assert.Equal(10, build.FindChildrenRecursive<Task>().Count);

            var items = build.FindChildrenRecursive<Item>().ToArray();
            // This is flaky because sometimes items will be in the tree and sometimes not
            // so the result could be 4 or 8
            //Assert.Equal(4, items.Length);
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

            AssertEx.EqualOrDiff(File.ReadAllText(xml1), File.ReadAllText(xml2));

            build = XlinqLogReader.ReadFromXml(xml1);
            Serialization.Write(build, GetTestFile("3.xml"));
            AssertEx.EqualOrDiff(File.ReadAllText(xml1), File.ReadAllText(GetTestFile("3.xml")));

            build = Serialization.Read(xml1);
            Serialization.Write(build, GetTestFile("4.xml"));

            AssertEx.EqualOrDiff(File.ReadAllText(xml1), File.ReadAllText(GetTestFile("4.xml")));
        }

        [Fact]
        public void TestReaderWriterRoundtripEquality()
        {
            var binLog = GetTestFile("1.binlog");
            var replayedBinlog = GetTestFile("1-replayed.binlog");
            File.Delete(replayedBinlog);

            //need to have in this repo
            var logReader = new Logging.StructuredLogger.BinaryLogReplayEventSource();

            BinaryLogger outputBinlog = new BinaryLogger()
            {
                Parameters = $"LogFile={replayedBinlog};OmitInitialInfo"
            };
            outputBinlog.Initialize(logReader);
            logReader.Replay(binLog);
            outputBinlog.Shutdown();

            //assert here
            AssertBinlogsHaveEqualContent(binLog, replayedBinlog);

            //TODO: temporarily disabling - as we do not have guarantee for binary equality of events
            // there are few mismatches between null and empty - will be fixed along with porting in-flight changes in MSBuild (needs log version update)
            //// If this assertation complicates development - it can possibly be removed
            //// The structured equality above should be enough.
            //AssertFilesAreBinaryEqualAfterUnpack(binLog, replayedBinlog);
        }

        private static void AssertFilesAreBinaryEqualAfterUnpack(string firstPath, string secondPath)
        {
            using var br1 = Logging.StructuredLogger.BinaryLogReplayEventSource.OpenReader(firstPath);
            using var br2 = Logging.StructuredLogger.BinaryLogReplayEventSource.OpenReader(secondPath);
            const int bufferSize = 4096;

            int readCount = 0;
            while (br1.ReadBytes(bufferSize) is { Length: > 0 } bytes1)
            {
                var bytes2 = br2.ReadBytes(bufferSize);

                bytes1.Should().BeEquivalentTo(bytes2,
                    $"Buffers starting at position {readCount} differ. First:{Environment.NewLine}{string.Join(",", bytes1)}{Environment.NewLine}Second:{Environment.NewLine}{string.Join(",", bytes2)}");
                readCount += bufferSize;
            }

            br2.ReadBytes(bufferSize).Length.Should().Be(0, "Second buffer contains bytes after first file end");
        }

        private static void AssertBinlogsHaveEqualContent(string firstPath, string secondPath)
        {
            using var reader1 = Logging.StructuredLogger.BinaryLogReplayEventSource.OpenBuildEventsReader(firstPath);
            using var reader2 = Logging.StructuredLogger.BinaryLogReplayEventSource.OpenBuildEventsReader(secondPath);

            Dictionary<string, string> embedFiles1 = new();
            Dictionary<string, string> embedFiles2 = new();

            reader1.ArchiveFileEncountered += arg
                => AddArchiveFile(embedFiles1, arg);
            reader2.ArchiveFileEncountered += arg
               => AddArchiveFile(embedFiles2, arg);


            int i = 0;
            while (reader1.Read() is { } ev1)
            {
                i++;
                var ev2 = reader2.Read();

                ev1.Should().BeEquivalentTo(ev2,
                    $"Binlogs ({firstPath} and {secondPath}) should be equal at event {i}");
            }
            // Read the second reader - to confirm there are no more events
            //  and to force the embedded files to be read.
            reader2.Read().Should().BeNull($"Binlogs ({firstPath} and {secondPath}) are not equal - second has more events >{i + 1}");

            Assert.Equal(embedFiles1, embedFiles2);
            embedFiles1.Should().NotBeEmpty();

            void AddArchiveFile(Dictionary<string, string> files, ArchiveFileEventArgs arg)
            {
                files.Add(arg.ArchiveFile.FullPath, arg.ArchiveFile.Text);
            }
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
