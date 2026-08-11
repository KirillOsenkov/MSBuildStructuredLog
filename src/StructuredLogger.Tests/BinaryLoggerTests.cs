using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogger.Tests;
using Xunit;
using Xunit.Abstractions;
using static StructuredLogger.Tests.TestUtilities;
using ArchiveFileEventArgs = Microsoft.Build.Logging.StructuredLogger.ArchiveFileEventArgs;
using BinaryLogger = Microsoft.Build.Logging.StructuredLogger.BinaryLogger;
using BinaryLogReaderErrorEventArgs = Microsoft.Build.Logging.StructuredLogger.BinaryLogReaderErrorEventArgs;
using BinaryLogRecordKind = Microsoft.Build.Logging.StructuredLogger.BinaryLogRecordKind;
using ReaderErrorType = Microsoft.Build.Logging.StructuredLogger.ReaderErrorType;

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

        [Fact]
        public void Replay_EventFilter_ProducesAValidCompactBinlog()
        {
            var metadataSeen = new List<BinaryLogEventMetadata>();
            var reader = new BinLogReader
            {
                EventFilter = metadata =>
                {
                    metadataSeen.Add(metadata);
                    return metadata.BuildEventContext == null ||
                           metadata.BuildEventContext.ProjectContextId == 101 ||
                           metadata.OriginalBuildEventContext?.ProjectContextId == 101;
                }
            };

            string outputPath = GetTestFile("filtered-replay.binlog");
            File.Delete(outputPath);

            var outputLogger = new BinaryLogger
            {
                Parameters = $"LogFile={outputPath};OmitInitialInfo",
                CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.None,
            };

            outputLogger.Initialize(reader);
            using (var input = CreateFilteredReplayStream())
            {
                reader.Replay(input);
            }
            outputLogger.Shutdown();

            metadataSeen.Should().Contain(metadata =>
                metadata.RecordKind == BinaryLogRecordKind.Warning &&
                metadata.BuildEventContext != null &&
                metadata.BuildEventContext.ProjectContextId == 202);
            metadataSeen.Should().Contain(metadata =>
                metadata.RecordKind == BinaryLogRecordKind.TargetSkipped &&
                metadata.OriginalBuildEventContext != null &&
                metadata.OriginalBuildEventContext.ProjectContextId == 101);

            var replayedEvents = new List<BuildEventArgs>();
            var outputReader = new BinLogReader();
            outputReader.AnyEventRaised += (_, args) => replayedEvents.Add(args);
            outputReader.Replay(outputPath);

            replayedEvents.Should().Contain(e =>
                e.GetType() == typeof(BuildMessageEventArgs) &&
                e.Message == "selected message");
            replayedEvents.Should().NotContain(e => e is BuildWarningEventArgs);
            replayedEvents.Should().ContainSingle(e => e is TargetSkippedEventArgs);

            var replayedStrings = new List<string>();
            var recordReader = new BinLogReader();
            recordReader.OnStringRead += (text, _) => replayedStrings.Add(text);
            using (var file = File.OpenRead(outputPath))
            using (var gzip = new System.IO.Compression.GZipStream(
                file,
                System.IO.Compression.CompressionMode.Decompress))
            using (var buffered = new BufferedStream(gzip, 32768))
            {
                recordReader.ReadRecordsFromDecompressedStream(buffered, includeAuxiliaryRecords: true)
                    .ToList();
            }

            replayedStrings.Should().NotContain("excluded warning");
            replayedStrings.Should().NotContain("WARN");
        }

        [Fact]
        public void Replay_EventFilter_PreservesTruncationRecoveryWhenRejectingEvents()
        {
            using var stream = CreateBinlogStream(withEndOfFileMarker: false);

            var reader = new BinLogReader
            {
                EventFilter = _ => false,
            };

            Action replay = () => reader.Replay(stream);

            replay.Should().NotThrow();
            reader.HasEncounteredTruncation.Should().BeTrue();
        }

        [Fact]
        public void Read_EventFilter_PreservesTruncationRecoveryInsideRejectedEvent()
        {
            byte[] full = WriteEventRecords(count: 6, withEndOfFileMarker: false);
            byte[] truncated = full.Take(full.Length / 2).ToArray();

            var memoryStream = new MemoryStream(truncated);
            var binaryReader = new BinaryReader(memoryStream);
            using var reader = new Logging.StructuredLogger.BuildEventArgsReader(
                binaryReader,
                BinaryLogger.FileFormatVersion);

            Action read = () =>
            {
                while (reader.Read(_ => false) != null)
                {
                }
            };

            read.Should().NotThrow();
            reader.ReachedEndOfStreamPrematurely.Should().BeTrue();
        }

        [Fact]
        public void Replay_EventFilter_RetainsRequiredCulturePreamble()
        {
            using var stream = CreateCulturePreambleStream();
            AssertCulturePreambleRetained(stream);
        }

        [Fact]
        public void Replay_EventFilter_RetainsRequiredCulturePreambleForLegacyFormat()
        {
            using var stream = CreateCulturePreambleStream(fileFormatVersion: 14);
            AssertCulturePreambleRetained(stream);
        }

        private static void AssertCulturePreambleRetained(Stream stream)
        {
            var replayedEvents = new List<BuildEventArgs>();
            var reader = new BinLogReader
            {
                EventFilter = _ => false,
            };
            reader.AnyEventRaised += (_, args) => replayedEvents.Add(args);

            reader.Replay(stream);

            var cultureMessages = replayedEvents
                .OfType<BuildMessageEventArgs>()
                .Where(message =>
                    message.SenderName == "BinaryLogger" &&
                    message.Message.StartsWith("CurrentUICulture", StringComparison.Ordinal))
                .ToList();
            cultureMessages.Should().ContainSingle();
        }

        [Fact]
        public void Replay_EventFilter_PropagatesFilterExceptionsWithoutLeakingWorkers()
        {
            var expected = new InvalidOperationException("filter failed");
            ThreadPool.GetAvailableThreads(out int availableWorkersBefore, out _);

            for (int i = 0; i < 8; i++)
            {
                using var stream = CreateFilteredReplayStream();
                var reader = new BinLogReader
                {
                    EventFilter = _ => throw expected,
                };

                Action replay = () => reader.Replay(stream);

                replay.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expected);
            }

            bool workersReleased = SpinWait.SpinUntil(
                () =>
                {
                    ThreadPool.GetAvailableThreads(out int availableWorkersAfter, out _);
                    return availableWorkersAfter >= availableWorkersBefore;
                },
                TimeSpan.FromSeconds(5));

            workersReleased.Should().BeTrue("filter failures must not leave replay workers blocked");
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

        // ---------------------------------------------------------------------------------------
        // Truncated / interrupted binlog recovery.
        //
        // A build that is hard-killed (test-run timeout killer, taskkill /F, CI job cancel, OOM,
        // crash) never runs BinaryLogger.Shutdown(), so its .binlog is missing the trailing
        // EndOfFile marker (and the gzip footer). The reader must recover every complete record
        // written before the cut, stop cleanly instead of throwing, and flag the log as truncated
        // so the viewer/CLI can tell the user the log was "recovered" and is "partial".
        // ---------------------------------------------------------------------------------------

        [Fact]
        public void InterruptedLog_ReadStopsCleanlyAndFlagsTruncation()
        {
            // No EndOfFile marker => the producing build was interrupted before it finalized the log.
            byte[] records = WriteEventRecords(count: 5, withEndOfFileMarker: false);

            var (recovered, truncated, thrown) = ReadAllEvents(records);

            thrown.Should().BeNull("a truncated log must stop cleanly, not throw");
            recovered.Should().Be(5, "every complete record written before the cut is still recoverable");
            truncated.Should().BeTrue("a stream that ends without an EndOfFile marker is a truncated log");
        }

        [Fact]
        public void FinalizedLog_ReadDoesNotFlagTruncation()
        {
            // A cleanly shut-down build terminates the record stream with an EndOfFile marker.
            byte[] records = WriteEventRecords(count: 5, withEndOfFileMarker: true);

            var (recovered, truncated, thrown) = ReadAllEvents(records);

            thrown.Should().BeNull();
            recovered.Should().Be(5);
            truncated.Should().BeFalse("an EndOfFile marker means the log was finalized - not truncated");
        }

        [Theory]
        [InlineData(0.90)]
        [InlineData(0.60)]
        [InlineData(0.35)]
        [InlineData(0.15)]
        public void TruncatedLog_ReadRecoversPrefixWithoutThrowing(double keptFraction)
        {
            // Cut the stream at an arbitrary offset to hit both record-boundary and mid-record
            // truncation. Whatever the cut, reading must never throw and must flag truncation.
            byte[] full = WriteEventRecords(count: 8, withEndOfFileMarker: true);
            int keep = Math.Max(1, (int)(full.Length * keptFraction));
            byte[] truncatedRecords = full.Take(keep).ToArray();

            var (recovered, truncated, thrown) = ReadAllEvents(truncatedRecords);

            thrown.Should().BeNull("mid-stream truncation must be handled without crashing");
            truncated.Should().BeTrue("a cut stream loses its EndOfFile marker");
            recovered.Should().BeInRange(0, 8);
        }

        [Fact]
        public void ReadRaw_TruncatedLog_ReturnsSyntheticEndOfFileWithoutThrowing()
        {
            // The raw record path backs BinlogTool `dumprecords` / ChunkBinlog, which used to crash
            // (0xE0434352) on a truncated log. It must now terminate at a synthetic EndOfFile.
            byte[] full = WriteEventRecords(count: 6, withEndOfFileMarker: false);
            byte[] truncatedRecords = full.Take(full.Length / 2).ToArray();

            var memoryStream = new MemoryStream(truncatedRecords);
            var binaryReader = new BinaryReader(memoryStream);
            using var reader = new Logging.StructuredLogger.BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion);

            Exception thrown = null;
            bool reachedEndOfFile = false;
            try
            {
                for (int guard = 0; guard < 10_000; guard++)
                {
                    var raw = reader.ReadRaw();
                    if (raw.RecordKind == BinaryLogRecordKind.EndOfFile)
                    {
                        reachedEndOfFile = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            thrown.Should().BeNull("the raw reader must stop cleanly on a truncated log instead of crashing");
            reachedEndOfFile.Should().BeTrue("raw reading terminates at a synthetic EndOfFile");
            reader.ReachedEndOfStreamPrematurely.Should().BeTrue();
        }

        [Fact]
        public void Replay_InterruptedLog_SetsHasEncounteredTruncation()
        {
            // End-to-end through the real seam: gzip + file header + BinLogReader.Replay.
            using var stream = CreateBinlogStream(withEndOfFileMarker: false);

            var reader = new BinLogReader();
            reader.Replay(stream);

            reader.HasEncounteredTruncation.Should().BeTrue();
        }

        [Fact]
        public void Replay_FinalizedLog_DoesNotSetTruncation()
        {
            using var stream = CreateBinlogStream(withEndOfFileMarker: true);

            var reader = new BinLogReader();
            reader.Replay(stream);

            reader.HasEncounteredTruncation.Should().BeFalse();
        }

        [Fact]
        public void ReadBuild_InterruptedLog_SurfacesRecoveredPartialError()
        {
            // The user-facing behavior: a truncated binlog opens as a build carrying exactly one
            // clear "recovered a partial binlog" error (message text mirrors BinaryLog.cs).
            using var stream = CreateBinlogStream(withEndOfFileMarker: false);

            var build = BinaryLog.ReadBuild(stream);

            build.Should().NotBeNull();
            var truncationErrors = build.FindChildrenRecursive<Microsoft.Build.Logging.StructuredLogger.Error>(
                e => e.Text != null && e.Text.StartsWith("Recovered a partial binlog"));
            truncationErrors.Should().ContainSingle("a truncated binlog is surfaced as a single 'recovered a partial binlog' error");
        }

        [Fact]
        public void ReadBuild_FinalizedLog_HasNoTruncationError()
        {
            using var stream = CreateBinlogStream(withEndOfFileMarker: true);

            var build = BinaryLog.ReadBuild(stream);

            build.Should().NotBeNull();
            var truncationErrors = build.FindChildrenRecursive<Microsoft.Build.Logging.StructuredLogger.Error>(
                e => e.Text != null && e.Text.StartsWith("Recovered a partial binlog"));
            truncationErrors.Should().BeEmpty("a finalized binlog must not report truncation");
        }

        // Writes `count` well-formed build event records as a raw record stream (no file header, no
        // gzip), mirroring the ForwardCompatibleRead_* tests. Optionally terminates with an EndOfFile
        // marker to represent a cleanly finalized log.
        private static byte[] WriteEventRecords(int count, bool withEndOfFileMarker)
        {
            var memoryStream = new MemoryStream();
            var binaryWriter = new BinaryWriter(memoryStream);
            var writer = new BuildEventArgsWriter(binaryWriter);

            for (int i = 0; i < count; i++)
            {
                writer.Write(CreateEvent(i));
            }

            binaryWriter.Flush();

            if (withEndOfFileMarker)
            {
                // The EndOfFile terminator is written straight to the underlying stream (as
                // BinaryLogger.Shutdown does with stream.WriteByte), not through the record-framing
                // writer, which buffers into a discarded per-record stream.
                memoryStream.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
            }

            return memoryStream.ToArray();
        }

        // Builds a complete gzip-compressed binlog stream (file header + records) the way
        // BinaryLogger writes one, optionally without the trailing EndOfFile marker to simulate a
        // hard-killed build. The gzip itself is valid; only the record stream inside is unterminated.
        private static Stream CreateBinlogStream(bool withEndOfFileMarker)
        {
            var decompressed = new MemoryStream();
            var binaryWriter = new BinaryWriter(decompressed);

            // File header: fileFormatVersion + minimumReaderVersion, both raw Int32 (v18+).
            binaryWriter.Write(BinaryLogger.FileFormatVersion);
            binaryWriter.Write(BinaryLogger.FileFormatVersion);

            var writer = new BuildEventArgsWriter(binaryWriter);
            writer.Write(new BuildStartedEventArgs("Build started", helpKeyword: null));
            for (int i = 0; i < 4; i++)
            {
                writer.Write(CreateEvent(i));
            }

            binaryWriter.Flush();

            if (withEndOfFileMarker)
            {
                // Terminator written directly to the stream, matching BinaryLogger.Shutdown.
                decompressed.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
            }

            decompressed.Position = 0;

            var compressed = new MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            {
                decompressed.CopyTo(gzip);
            }

            compressed.Position = 0;
            return compressed;
        }

        private static Stream CreateFilteredReplayStream()
        {
            var selectedContext = new BuildEventContext(1, 1, 1, 1, 101, 11, 111);
            var excludedContext = new BuildEventContext(1, 1, 2, 2, 202, 22, 222);
            var skippedContext = new BuildEventContext(1, 1, 3, 3, 303, 33, 333);

            var selectedMessage = new BuildMessageEventArgs(
                "selected message", "Help", "Sender", MessageImportance.Normal)
            {
                BuildEventContext = selectedContext,
            };

            var excludedWarning = new BuildWarningEventArgs(
                "Subcategory", "WARN", "File.cs", 1, 2, 3, 4,
                "excluded warning", "Help", "Sender")
            {
                BuildEventContext = excludedContext,
            };

            var targetSkipped = new TargetSkippedEventArgs("target skipped")
            {
                BuildEventContext = skippedContext,
                OriginalBuildEventContext = selectedContext,
                TargetName = "SkippedTarget",
                TargetFile = "Targets.targets",
                ProjectFile = "Project.csproj",
                BuildReason = TargetBuiltReason.DependsOn,
                SkipReason = TargetSkipReason.PreviouslyBuiltSuccessfully,
                OriginallySucceeded = true,
            };

            var decompressed = new MemoryStream();
            var binaryWriter = new BinaryWriter(decompressed);
            binaryWriter.Write(BinaryLogger.FileFormatVersion);
            binaryWriter.Write(BinaryLogger.FileFormatVersion);

            var writer = new BuildEventArgsWriter(binaryWriter);
            writer.Write(new BuildStartedEventArgs("Build started", helpKeyword: null));
            writer.Write(selectedMessage);
            writer.Write(excludedWarning);
            writer.Write(targetSkipped);
            writer.Write(new BuildFinishedEventArgs("Build finished", helpKeyword: null, succeeded: true));
            binaryWriter.Flush();
            decompressed.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
            decompressed.Position = 0;

            var compressed = new MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(
                compressed,
                System.IO.Compression.CompressionLevel.Optimal,
                leaveOpen: true))
            {
                decompressed.CopyTo(gzip);
            }

            compressed.Position = 0;
            return compressed;
        }

        private static Stream CreateCulturePreambleStream(
            int fileFormatVersion = BinaryLogger.FileFormatVersion)
        {
            var framedRecords = new MemoryStream();
            var framedWriter = new BinaryWriter(framedRecords);
            var writer = new BuildEventArgsWriter(framedWriter);
            writer.Write(new BuildMessageEventArgs(
                "CurrentUICulture=en-US",
                helpKeyword: null,
                senderName: "BinaryLogger",
                importance: MessageImportance.Low));
            writer.Write(new BuildMessageEventArgs(
                "filtered message",
                helpKeyword: null,
                senderName: "Sender",
                importance: MessageImportance.Normal));
            framedWriter.Flush();
            framedRecords.WriteByte((byte)BinaryLogRecordKind.EndOfFile);
            framedRecords.Position = 0;

            var decompressed = new MemoryStream();
            var binaryWriter = new BinaryWriter(decompressed);
            binaryWriter.Write(fileFormatVersion);
            if (fileFormatVersion >= BinaryLogger.ForwardCompatibilityMinimalVersion)
            {
                binaryWriter.Write(fileFormatVersion);
                framedRecords.CopyTo(decompressed);
            }
            else
            {
                CopyRecordsWithoutLengths(framedRecords, binaryWriter);
            }

            binaryWriter.Flush();
            decompressed.Position = 0;

            var compressed = new MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(
                compressed,
                System.IO.Compression.CompressionLevel.Optimal,
                leaveOpen: true))
            {
                decompressed.CopyTo(gzip);
            }

            compressed.Position = 0;
            return compressed;
        }

        private static void CopyRecordsWithoutLengths(
            Stream framedRecords,
            BinaryWriter legacyWriter)
        {
            using var framedReader = new BinaryReader(framedRecords);

            while (true)
            {
                int recordKindValue = Serialization.Read7BitEncodedInt(framedReader);
                var recordKind = (BinaryLogRecordKind)recordKindValue;
                Serialization.Write7BitEncodedInt(legacyWriter, recordKindValue);

                if (recordKind == BinaryLogRecordKind.EndOfFile)
                {
                    return;
                }

                if (recordKind == BinaryLogRecordKind.String)
                {
                    legacyWriter.Write(framedReader.ReadString());
                    continue;
                }

                recordKind.Should().Be(
                    BinaryLogRecordKind.Message,
                    "the legacy culture test stream contains only strings and messages");

                int recordLength = Serialization.Read7BitEncodedInt(framedReader);
                byte[] record = framedReader.ReadBytes(recordLength);
                if (record.Length != recordLength)
                {
                    throw new EndOfStreamException();
                }

                legacyWriter.Write(record);
            }
        }

        private static BuildEventArgs CreateEvent(int i) => (i % 3) switch
        {
            0 => new BuildMessageEventArgs($"message {i} with a reasonably long payload so records span several bytes", "Help", "Sender", MessageImportance.Normal),
            1 => new BuildErrorEventArgs("Subcategory", $"CODE{i}", "File.cs", i + 1, i + 2, i + 3, i + 4, $"error message {i}", "Help", "Sender"),
            _ => new BuildWarningEventArgs("Subcategory", $"WARN{i}", "File.cs", i + 1, i + 2, i + 3, i + 4, $"warning message {i}", "Help", "Sender"),
        };

        private static (int recovered, bool truncated, Exception thrown) ReadAllEvents(byte[] recordBytes)
        {
            var memoryStream = new MemoryStream(recordBytes);
            var binaryReader = new BinaryReader(memoryStream);
            using var reader = new Logging.StructuredLogger.BuildEventArgsReader(binaryReader, BinaryLogger.FileFormatVersion);

            int recovered = 0;
            Exception thrown = null;
            try
            {
                while (reader.Read() != null)
                {
                    recovered++;
                }
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            return (recovered, reader.ReachedEndOfStreamPrematurely, thrown);
        }
    }
}
