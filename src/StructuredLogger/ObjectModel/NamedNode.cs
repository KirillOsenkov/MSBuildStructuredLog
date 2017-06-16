namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NamedNode : TreeNode
    {
        public string Name { get; set; }

        public virtual string LookupKey => Name;
        public override string ToString() => Name;
    }
}
