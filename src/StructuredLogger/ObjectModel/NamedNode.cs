namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NamedNode : TreeNode, IHasTitle
    {
        public virtual string Name { get; set; }
        public string ShortenedName => TextUtilities.ShortenValue(Name);
        public bool IsNameShortened => Name != ShortenedName;

        public virtual string LookupKey => Name;
        string IHasTitle.Title => GetTitle();
        protected virtual string GetTitle() => Name;
        public override string TypeName => nameof(NamedNode);

        public override string ToString() => Name;
    }
}
