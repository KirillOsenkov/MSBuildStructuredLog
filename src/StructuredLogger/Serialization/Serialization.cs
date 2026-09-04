using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public static class Serialization
    {
        public static readonly string FileDialogFilter = "Structured Log (*.buildlog)|*.buildlog|Readable (large) XML Log (*.xml)|*.xml";
        public static readonly string BinlogFileDialogFilter = "Binary Log (*.binlog)|*.binlog|Structured Log (*.buildlog)|*.buildlog|Readable (large) XML Log (*.xml)|*.xml";
        public static readonly string OpenFileDialogFilter = "Build Log (*.binlog;*.buildlog;*.xml)|*.binlog;*.buildlog;*.xml";
        public static readonly string OpenFileGraphFilter = "Graph (*.dot;*.txt)|*.dot;*.txt";

        public static readonly XName[] AttributeNameList = typeof(AttributeNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => XNamespace.None.GetName(f.Name)).ToArray();

        public static readonly string[] AttributeLocalNameList = typeof(AttributeNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name).ToArray();

        public static Build ReadXmlLog(Stream stream) => XmlLogReader.ReadFromXml(stream);
        public static Build ReadBuildLog(Stream stream, byte[] projectImportsArchive = null) => BuildLogReader.Read(stream, projectImportsArchive);
        public static Build ReadBinLog(Stream stream, byte[] projectImportsArchive = null) => BinaryLog.ReadBuild(stream, projectImportsArchive);

        public static Build Read(string filePath) => Read(filePath, progress: null, readerSettings: ReaderSettings.Default);

        public static Build Read(string filePath, Progress progress) => Read(filePath, progress, readerSettings: ReaderSettings.Default);

        public static Build Read(string filePath, Progress progress, ReaderSettings readerSettings)
        {
            if (filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return XmlLogReader.ReadFromXml(filePath);
            }
            else if (filePath.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return BinaryLog.ReadBuild(filePath, progress, readerSettings);
                }
                catch (Exception)
                {
                    var format = DetectLogFormat(filePath);
                    if (format == ".buildlog")
                    {
                        return BuildLogReader.Read(filePath);
                    }
                    else if (format == "1.2")
                    {
                        return ReadOld1_2FormatBuild(filePath);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else if (filePath.EndsWith(".buildlog", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return BuildLogReader.Read(filePath);
                }
                catch (Exception)
                {
                    if (DetectLogFormat(filePath) == ".binlog")
                    {
                        return BinaryLog.ReadBuild(filePath, progress, readerSettings);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return null;
        }

        private static Build ReadOld1_2FormatBuild(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                var b1 = stream.ReadByte();
                var b2 = stream.ReadByte();
                var b3 = stream.ReadByte();

                return BuildLogReader.Read(stream, projectImportsArchive: null, new Version(b1, b2, b3));
            }
        }

        public static string DetectLogFormat(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                if (stream.Length < 4)
                {
                    return null;
                }

                var b1 = stream.ReadByte();
                var b2 = stream.ReadByte();
                if (b1 == 0x1F && b2 == 0x8B)
                {
                    return ".binlog";
                }

                if (b1 == 1 && b2 == 2)
                {
                    return "1.2";
                }

                if (b1 == 0x1)
                {
                    return ".buildlog";
                }
            }

            return null;
        }

        public static void Write(Build build, string filePath)
        {
            if (filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                XmlLogWriter.WriteToXml(build, filePath);
            }
            else
            {
                BuildLogWriter.Write(build, filePath);
            }
        }

        public static string GetNodeName(BaseNode node)
        {
            var folder = node as Folder;
            if (folder != null && IsValidXmlElementName(folder.Name))
            {
                return folder.Name;
            }

            return node.GetType().Name;
        }

        public static bool IsValidXmlElementName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (!char.IsLetter(name[0]))
            {
                return false;
            }

            if (name.Any(c => !char.IsLetterOrDigit(c)))
            {
                return false;
            }

            return true;
        }

        public static BaseNode CreateNode(string name)
        {
            switch (name)
            {
                case nameof(NameValueNode): return new NameValueNode();
                case nameof(NamedNode): return new NamedNode();
                case nameof(TextNode): return new TextNode();
                case nameof(TimedNode): return new TimedNode();
                case nameof(AddOrRemoveItem): return new AddOrRemoveItem();
                case nameof(AddItem): return new AddItem();
                case nameof(TaskParameterItem): return new TaskParameterItem();
                case nameof(RemoveItem): return new RemoveItem();
                case nameof(AbstractDiagnostic): return new AbstractDiagnostic();
                case nameof(Error): return new Error();
                case nameof(BuildError): return new BuildError();
                case nameof(Warning): return new Warning();
                case nameof(CriticalBuildMessage): return new CriticalBuildMessage();
                case nameof(Build): return new Build();
                case nameof(EntryTarget): return new EntryTarget();
                case nameof(EvaluationProfileEntry): return new EvaluationProfileEntry();
                case nameof(Folder): return new Folder();
                case nameof(Import): return new Import();
                case nameof(Item): return new Item();
                case nameof(FileCopy): return new FileCopy();
                case nameof(Message): return new Message();
                case nameof(TimedMessage): return new TimedMessage();
                case nameof(MessageWithLocation): return new MessageWithLocation();
                case nameof(PropertyAssignmentMessage): return new PropertyAssignmentMessage();
                case nameof(PropertyInitialAssignmentMessage): return new PropertyInitialAssignmentMessage();
                case nameof(PropertyReassignmentMessage): return new PropertyReassignmentMessage();
                case nameof(MSBuildServerNode): return new MSBuildServerNode();
                case nameof(Metadata): return new Metadata();
                case nameof(NoImport): return new NoImport();
                case nameof(Note): return new Note();
                case nameof(Package): return new Package();
                case nameof(Parameter): return new Parameter();
                case nameof(Project): return new Project();
                case nameof(ProjectEvaluation): return new ProjectEvaluation();
                case nameof(Property): return new Property();
                case nameof(TaskParameterProperty): return new TaskParameterProperty();
                case nameof(SearchableItem): return new SearchableItem();
                case nameof(SourceFile): return new SourceFile();
                case nameof(SourceFileLine): return new SourceFileLine();
                case nameof(SourceFileLineWithHighlights): return new SourceFileLineWithHighlights();
                case nameof(Target): return new Target();
                case nameof(Task): return new Task();
                case nameof(PlaceholderTask): return new PlaceholderTask();
                case nameof(MSBuildTask): return new MSBuildTask();
                case nameof(CopyTask): return new CopyTask();
                case nameof(ManagedCompilerTask): return new ManagedCompilerTask();
                case nameof(CscTask): return new CscTask();
                case nameof(FscTask): return new FscTask();
                case nameof(VbcTask): return new VbcTask();
                case nameof(ResolveAssemblyReferenceTask): return new ResolveAssemblyReferenceTask();
                case nameof(RobocopyTask): return new RobocopyTask();
                case nameof(CppAnalyzer.CppTask): return new CppAnalyzer.CppTask();
                case nameof(ProxyNode): return new ProxyNode();
                default: return new Folder();
            }
        }

        public static bool GetBoolean(string text)
        {
            if (text == null)
            {
                return false;
            }

            bool result;
            bool.TryParse(text, out result);
            return result;
        }

        public static DateTime GetDateTime(string text)
        {
            if (text == null)
            {
                return default(DateTime);
            }

            DateTime result;
            DateTime.TryParse(text, out result);
            return result;
        }

        public static int GetInteger(string text)
        {
            if (text == null)
            {
                return 0;
            }

            int result;
            int.TryParse(text, out result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read7BitEncodedInt(this BinaryReader reader)
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                {
                    throw new FormatException();
                }

                // ReadByte handles end of stream cases for us.
                b = reader.ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write7BitEncodedInt(this BinaryWriter writer, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers
            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }

            writer.Write((byte)v);
        }

        public static void WriteStringsToFile(string outputFilePath, string[] strings)
        {
            using var fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
            using var bufferedStream = new BufferedStream(gzipStream);
            using var binaryWriter = new BinaryWriter(bufferedStream);

            binaryWriter.Write(strings.Length);

            for (int i = 0; i < strings.Length; i++)
            {
                binaryWriter.Write(strings[i]);
            }
        }

        public static IReadOnlyList<string> ReadStringsFromFile(string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var bufferedStream = new BufferedStream(gzipStream);
            using var binaryReader = new BinaryReader(bufferedStream);

            int count = binaryReader.ReadInt32();
            var result = new string[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = binaryReader.ReadString();
            }

            return result;
        }
    }
}
