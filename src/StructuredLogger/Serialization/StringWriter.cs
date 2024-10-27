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

        private static void WriteNode(BaseNode node, StringBuilder sb, int indent = 0)
        {
            if (node == null)
            {
                return;
            }

            if (sb.Length > MaxStringLength)
            {
                return;
            }

            Indent(sb, indent);

            var text = node.GetFullText();

            sb.AppendLine(text);

            var treeNode = node as TreeNode;
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
