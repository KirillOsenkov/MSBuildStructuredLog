using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogger.Tests;
using Xunit;
using Xunit.Abstractions;
using static StructuredLogger.Tests.TestUtilities;
using BinaryLogger = Microsoft.Build.Logging.StructuredLogger.BinaryLogger;

namespace Microsoft.Build.UnitTests
{
    public class BinaryLoggerTests
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
        private bool BuildProject(string projectFile, string binLog, bool useInMemoryProject)
        {
            File.Delete(binLog);
            var binaryLogger = new BinaryLogger { Parameters = binLog };
            return useInMemoryProject
                ? MSBuild.BuildProjectInMemory(projectFile, binaryLogger)
                : MSBuild.BuildProjectFromFile(projectFile, binaryLogger);
        }

        [Fact]
        public void TestBuildTreeStructureCount()
        {
            var binLog = GetTestFile("1.binlog");
            Assert.True(BuildProject(s_testProject, binLog, false));

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
            Assert.True(BuildProject(s_testProject, binLog, useInMemoryProject));

            var build = Serialization.Read(binLog);
            var xml1 = GetTestFile("1.xml");
            var xml2 = GetTestFile("2.xml");

            build.Children.Where(c => c.TypeName == "Error").Should().BeEmpty("There should be no errors in the build");

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
            Assert.True(BuildProject(s_testProject, binLog, false));
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

            // If this assertation complicates development - it can possibly be removed
            // The structured equality above should be enough.
            AssertFilesAreBinaryEqualAfterUnpack(binLog, replayedBinlog);
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

            // Pull events from both logs simultaneously and compare them
            int i = 0;
            while (reader1.Read() is { } logRecord1)
            {
                i++;
                var logRecord2 = reader2.Read();

                logRecord1.Should().BeEquivalentTo(logRecord2,
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

        [Fact]
        public void ForwardCompatibleRead_HandleAppendOnlyChanges()
        {
            // Let's not write any strings prior the first event - to make locating (and overwriting) the size byte(s) easier.
            BuildErrorEventArgs error = new(null, null, null, 1, 2, 3, 4, null, null, null);
            BuildFinishedEventArgs finished = new("Message", "HelpKeyword", true);

            var memoryStream = new MemoryStream();
            var binaryWriter = new BinaryWriter(memoryStream);
            var binaryReader = new BinaryReader(memoryStream);
            var buildEventArgsWriter = new BuildEventArgsWriter(binaryWriter);

            buildEventArgsWriter.Write(error);

            // Some future data that are not known in current version
            binaryWriter.Write(new byte[] { 1, 2, 3, 4 });


            int positionAfterFirstEvent = (int)memoryStream.Position;
            memoryStream.Position = 0;
            // event type
            Serialization.Read7BitEncodedInt(binaryReader);
            int eventSizePos = (int)memoryStream.Position;
            int eventSize = Serialization.Read7BitEncodedInt(binaryReader);
            int positionAfterFirstEventSize = (int)memoryStream.Position;
            memoryStream.Position = eventSizePos;
            // the extra 4 bytes
            Serialization.Write7BitEncodedInt(binaryWriter, eventSize + 4);
            memoryStream.Position.Should().Be(positionAfterFirstEventSize, "The event size need to be overwritten in place - without overwriting any bytes after the size info");
            memoryStream.Position = positionAfterFirstEvent;

            buildEventArgsWriter.Write(finished);

            // Remember num of bytes written - we should read them all.
            long length = memoryStream.Length;
            // Now move back to the beginning of the stream and start reading.
            memoryStream.Position = 0;

            using var buildEventArgsReader = new Logging.StructuredLogger.BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion)
            {
                SkipUnknownEventParts = true
            };

            List<BinaryLogReaderErrorEventArgs> readerErrors = new();
            buildEventArgsReader.RecoverableReadError += readerErrors.Add;

            var deserializedError = (BuildErrorEventArgs)buildEventArgsReader.Read();

            readerErrors.Count.Should().Be(1);
            readerErrors[0].ErrorType.Should().Be(ReaderErrorType.UnknownEventData);
            readerErrors[0].RecordKind.Should().Be(BinaryLogRecordKind.Error);

            deserializedError.Should().BeEquivalentTo(error);

            var deserializedFinished = (BuildFinishedEventArgs)buildEventArgsReader.Read();

            readerErrors.Count.Should().Be(1);

            deserializedFinished.Should().BeEquivalentTo(finished);

            // There is nothing else in the stream
            memoryStream.Position.Should().Be(length);
        }

        [Fact]
        public void ForwardCompatibleRead_HandleUnknownEvent()
        {
            // Let's not write any strings prior the first event - to make locating (and overwriting) the event type byte(s) easier.
            BuildErrorEventArgs error = new(null, null, null, 1, 2, 3, 4, null, null, null);
            BuildFinishedEventArgs finished = new("Message", "HelpKeyword", true);

            var memoryStream = new MemoryStream();
            var binaryWriter = new BinaryWriter(memoryStream);
            var binaryReader = new BinaryReader(memoryStream);
            var buildEventArgsWriter = new BuildEventArgsWriter(binaryWriter);

            buildEventArgsWriter.Write(error);

            int positionAfterFirstEvent = (int)memoryStream.Position;
            memoryStream.Position = 0;
            // event type
            Serialization.Read7BitEncodedInt(binaryReader);
            int eventSizePos = (int)memoryStream.Position;
            memoryStream.Position = 0;

            // some future type that is not known in current version
            BinaryLogRecordKind unknownType = (BinaryLogRecordKind)Enum.GetValues(typeof(BinaryLogRecordKind)).Cast<BinaryLogRecordKind>().Select(e => (int)e).Max() + 2;
            Serialization.Write7BitEncodedInt(binaryWriter, (int)unknownType);
            memoryStream.Position.Should().Be(eventSizePos, "The event type need to be overwritten in place - without overwriting any bytes after the type info");
            memoryStream.Position = positionAfterFirstEvent;

            buildEventArgsWriter.Write(finished);

            // Remember num of bytes written - we should read them all.
            long length = memoryStream.Length;
            // Now move back to the beginning of the stream and start reading.
            memoryStream.Position = 0;

            List<BinaryLogReaderErrorEventArgs> readerErrors = new();
            using var buildEventArgsReader = new Logging.StructuredLogger.BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion)
            {
                SkipUnknownEvents = true
            };

            buildEventArgsReader.RecoverableReadError += readerErrors.Add;

            var deserializedEvent = buildEventArgsReader.Read();

            readerErrors.Count.Should().Be(1);
            readerErrors[0].ErrorType.Should().Be(ReaderErrorType.UnknownEventType);
            readerErrors[0].RecordKind.Should().Be(unknownType);

            deserializedEvent.Should().BeEquivalentTo(finished);

            // There is nothing else in the stream
            memoryStream.Position.Should().Be(length);
        }

        [Fact]
        public void ForwardCompatibleRead_HandleMismatchedFormatOfEvent()
        {
            // BuildErrorEventArgs error = new("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "SenderName");
            BuildErrorEventArgs error = new(null, null, null, 1, 2, 3, 4, null, null, null);
            BuildFinishedEventArgs finished = new("Message", "HelpKeyword", true);

            var memoryStream = new MemoryStream();
            var binaryWriter = new BinaryWriter(memoryStream);
            var binaryReader = new BinaryReader(memoryStream);
            var buildEventArgsWriter = new BuildEventArgsWriter(binaryWriter);

            buildEventArgsWriter.Write(error);

            int positionAfterFirstEvent = (int)memoryStream.Position;
            memoryStream.Position = 0;
            // event type
            Serialization.Read7BitEncodedInt(binaryReader);
            int eventSize = Serialization.Read7BitEncodedInt(binaryReader);
            // overwrite the entire event with garbage
            binaryWriter.Write(Enumerable.Repeat(byte.MaxValue, eventSize).ToArray());

            memoryStream.Position.Should().Be(positionAfterFirstEvent, "The event need to be overwritten in place - without overwriting any bytes after the size info");

            buildEventArgsWriter.Write(finished);

            // Remember num of bytes written - we should read them all.
            long length = memoryStream.Length;
            // Now move back to the beginning of the stream and start reading.
            memoryStream.Position = 0;

            using var buildEventArgsReader = new Logging.StructuredLogger.BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion)
            {
                SkipUnknownEvents = true
            };

            List<BinaryLogReaderErrorEventArgs> readerErrors = new();
            buildEventArgsReader.RecoverableReadError += readerErrors.Add;

            var deserializedEvent = buildEventArgsReader.Read();

            readerErrors.Count.Should().Be(1);
            readerErrors[0].ErrorType.Should().Be(ReaderErrorType.UnknownFormatOfEventData);
            readerErrors[0].RecordKind.Should().Be(BinaryLogRecordKind.Error);
            readerErrors[0].GetFormattedMessage().Should().Contain("FormatException");

            deserializedEvent.Should().BeEquivalentTo(finished);

            // There is nothing else in the stream
            memoryStream.Position.Should().Be(length);
        }

        [Fact]
        public void ForwardCompatibleRead_HandleRemovalOfDataFromEventDefinition()
        {
            // BuildErrorEventArgs error = new("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "SenderName");
            BuildErrorEventArgs error = new(null, null, null, 1, 2, 3, 4, null, null, null);
            BuildFinishedEventArgs finished = new("Message", "HelpKeyword", true);

            var memoryStream = new MemoryStream();
            var binaryWriter = new BinaryWriter(memoryStream);
            var binaryReader = new BinaryReader(memoryStream);
            var buildEventArgsWriter = new BuildEventArgsWriter(binaryWriter);

            buildEventArgsWriter.Write(error);

            int positionAfterFirstEvent = (int)memoryStream.Position;
            memoryStream.Position = 0;
            // event type
            Serialization.Read7BitEncodedInt(binaryReader);
            int eventSizePos = (int)memoryStream.Position;
            int eventSize = Serialization.Read7BitEncodedInt(binaryReader);
            int positionAfterFirstEventSize = (int)memoryStream.Position;
            memoryStream.Position = eventSizePos;
            // simulate there are 4 bytes less in the future version of the event - while our reader expects those
            Serialization.Write7BitEncodedInt(binaryWriter, eventSize - 4);
            memoryStream.Position.Should().Be(positionAfterFirstEventSize, "The event size need to be overwritten in place - without overwriting any bytes after the size info");
            // remove the 4 bytes - so that actual size of event is inline with it's written size.
            memoryStream.Position = positionAfterFirstEvent - 4;

            buildEventArgsWriter.Write(finished);

            // Remember num of bytes written - we should read them all.
            long length = memoryStream.Length;
            // Now move back to the beginning of the stream and start reading.
            memoryStream.Position = 0;

            using var buildEventArgsReader = new Logging.StructuredLogger.BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion)
            {
                SkipUnknownEvents = true
            };

            List<BinaryLogReaderErrorEventArgs> readerErrors = new();
            buildEventArgsReader.RecoverableReadError += readerErrors.Add;

            var deserializedEvent = buildEventArgsReader.Read();

            readerErrors.Count.Should().Be(1);
            readerErrors[0].ErrorType.Should().Be(ReaderErrorType.UnknownFormatOfEventData);
            readerErrors[0].RecordKind.Should().Be(BinaryLogRecordKind.Error);
            readerErrors[0].GetFormattedMessage().Should().Contain("EndOfStreamException");

            deserializedEvent.Should().BeEquivalentTo(finished);

            // There is nothing else in the stream
            memoryStream.Position.Should().Be(length);
        }
    }
}
