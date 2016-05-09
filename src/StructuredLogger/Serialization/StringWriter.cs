using System.Linq;
using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class StringWriter
    {
        public static string GetString(TreeNode rootNode)
        {
            var sb = new StringBuilder();

            WriteNode(rootNode, sb, 0);

            return sb.ToString();
        }

        private static void WriteNode(TreeNode rootNode, StringBuilder sb, int indent = 0)
        {
            Indent(sb, indent);
            sb.AppendLine(rootNode.ToString());

            if (rootNode.HasChildren)
            {
                foreach (var child in rootNode.Children.OfType<TreeNode>())
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
