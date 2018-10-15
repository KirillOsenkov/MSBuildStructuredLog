namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NamedNode : TreeNode, IHasTitle
    {
        public string Name { get; set; }

        public virtual string LookupKey => Name;
        string IHasTitle.Title => Name;

        public override string ToString() => Name;
    }
}
