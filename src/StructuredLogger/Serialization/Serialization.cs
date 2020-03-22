using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Serialization
    {
        public static readonly string FileDialogFilter = "Binary (compact) Structured Build Log (*.buildlog)|*.buildlog|Readable (large) XML Log (*.xml)|*.xml";
        public static readonly string OpenFileDialogFilter = "Build Log (*.binlog;*.buildlog;*.xml)|*.binlog;*.buildlog;*.xml";

        public static readonly XName[] AttributeNameList = typeof(AttributeNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => XNamespace.None.GetName(f.Name)).ToArray();

        public static readonly string[] AttributeLocalNameList = typeof(AttributeNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name).ToArray();

        private static Dictionary<string, Type> objectModelTypes;
        public static Dictionary<string, Type> ObjectModelTypes
        {
            get
            {
                if (objectModelTypes == null)
                {
                    objectModelTypes = typeof(TreeNode)
                        .GetTypeInfo()
                        .Assembly
                        .GetTypes()
                        .Where(t => typeof(BaseNode).IsAssignableFrom(t))
                        .ToDictionary(t => t.Name);
                }

                return objectModelTypes;
            }
        }

        public static Build ReadXmlLog(Stream stream) => XmlLogReader.ReadFromXml(stream);
        public static Build ReadBuildLog(Stream stream, byte[] projectImportsArchive = null) => BuildLogReader.Read(stream, projectImportsArchive);
        public static Build ReadBinLog(Stream stream, byte[] projectImportsArchive = null) => BinaryLog.ReadBuild(stream, projectImportsArchive);

        public static Build Read(string filePath)
        {
            if (filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return XmlLogReader.ReadFromXml(filePath);
            }
            else if (filePath.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return BinaryLog.ReadBuild(filePath);
                }
                catch (Exception)
                {
                    if (DetectLogFormat(filePath) == ".buildlog")
                    {
                        return BuildLogReader.Read(filePath);
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
                        return BinaryLog.ReadBuild(filePath);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return null;
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
            Type type = null;
            if (!ObjectModelTypes.TryGetValue(name, out type))
            {
                type = typeof(Folder);
            }

            var node = (BaseNode)Activator.CreateInstance(type);
            return node;
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
    }
}
