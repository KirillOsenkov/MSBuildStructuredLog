namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NamedNode : TreeNode, IHasTitle
    {
        public string Name { get; set; }

        public virtual string LookupKey => Name;
        string IHasTitle.Title => GetTitle();
        protected virtual string GetTitle() => Name;
        public override string TypeName => nameof(NamedNode);

        public override string ToString() => Name;
    }
}
