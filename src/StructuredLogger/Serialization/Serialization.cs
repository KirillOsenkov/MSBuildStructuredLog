using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Serialization
    {
        public static readonly XName[] AttributeNameList = typeof(AttributeNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => XNamespace.None.GetName(f.Name)).ToArray();

        public static readonly string[] AttributeLocalNameList = typeof(AttributeNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name).ToArray();

        public static readonly Dictionary<string, Type> ObjectModelTypes =
            typeof(TreeNode)
                .Assembly
                .GetTypes()
                .Where(t => typeof(TreeNode).IsAssignableFrom(t))
                .ToDictionary(t => t.Name);
    }
}
