namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Package : NamedNode
    {
        public override string TypeName => nameof(Package);

        public string Version { get; set; }

        public string VersionSpec { get; set; }

        public override string ToString() => TextUtilities.Separate(" ", Name, Version, VersionSpec);

        // BaseNode.GetFullText() falls through to Title (just Name) for
        // NamedNode subclasses, dropping Version/VersionSpec. Used by
        // StringWriter (Copy All / Copy Subtree), BinlogTool's `search` CLI,
        // and BinlogMcp's `search`. The visual tree renders Name and version
        // as separate TextBlocks (Generic.xaml ImportStroke binding) and is
        // unaffected.
        public override string GetFullText() => ToString();
    }
}