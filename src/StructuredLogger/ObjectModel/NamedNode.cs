namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NamedNode : TreeNode
    {
        public virtual string Name { get; set; }
        public string ShortenedName => TextUtilities.ShortenValue(Name);
        public bool IsNameShortened => Name != ShortenedName;

        public override string TypeName => nameof(NamedNode);
        public override string Title => Name;

        public override string ToString() => Name;
    }
}
