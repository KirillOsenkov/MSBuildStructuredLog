using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class StringWriter
    {
        public static int MaxStringLength = 100_000_000;

        public static string GetString(BaseNode rootNode)
        {
            var sb = new StringBuilder();

            WriteNode(rootNode, sb, 0);

            return sb.ToString();
        }

        private static void WriteNode(BaseNode rootNode, StringBuilder sb, int indent = 0)
        {
            if (rootNode == null)
            {
                return;
            }

            if (sb.Length > MaxStringLength)
            {
                return;
            }

            Indent(sb, indent);
            var text = rootNode.ToString() ?? "";

            // when we ingest strings we normalize on \n to save space.
            // when the strings leave our app via clipboard, bring \r\n back so that notepad works
            text = text.Replace("\n", "\r\n");

            sb.AppendLine(text);

            var treeNode = rootNode as TreeNode;
            if (treeNode != null && treeNode.HasChildren)
            {
                foreach (var child in treeNode.Children)
                {
                    WriteNode(child, sb, indent + 1);
                }
            }
        }

        private static void Indent(StringBuilder sb, int indent)
        {
            for (int i = 0; i < indent * 4; i++)
            {
                sb.Append(' ');
            }
        }
    }
}
