using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Serialization
    {
        public static readonly string FileDialogFilter = "Binary (compact) Structured Build Log (*.buildlog)|*.buildlog|Readable (large) XML Log (*.xml)|*.xml";
        public static readonly string OpenFileDialogFilter = "Structured Build Log (*.buildlog;*.xml)|*.buildlog;*.xml";

        public static readonly XName[] AttributeNameList = typeof(AttributeNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => XNamespace.None.GetName(f.Name)).ToArray();

        public static readonly string[] AttributeLocalNameList = typeof(AttributeNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name).ToArray();

        public static readonly Dictionary<string, Type> ObjectModelTypes =
            typeof(TreeNode)
                .GetTypeInfo()
                .Assembly
                .GetTypes()
                .Where(t => typeof(BaseNode).IsAssignableFrom(t))
                .ToDictionary(t => t.Name);

        public static Build Read(string filePath)
        {
            if (filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return XmlLogReader.ReadFromXml(filePath);
            }
            else
            {
                return BinaryLogReader.Read(filePath);
            }
        }

        public static void Write(Build build, string filePath)
        {
            if (filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                XmlLogWriter.WriteToXml(build, filePath);
            }
            else
            {
                BinaryLogWriter.Write(build, filePath);
            }
        }

        public static string GetNodeName(object node)
        {
            var folder = node as Folder;
            if (folder != null && folder.Name != null)
            {
                return folder.Name;
            }

            return node.GetType().Name;
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
