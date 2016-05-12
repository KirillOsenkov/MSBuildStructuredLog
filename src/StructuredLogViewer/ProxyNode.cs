namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ProxyNode : TextNode
    {
        public object Original { get; set; }

        public ProxyNode()
        {
        }

        public ProxyNode(object original)
        {
            Original = original;
            this.Text = original.ToString();
        }

        public override string ToString()
        {
            return Name ?? Original.ToString();
        }
    }
}
