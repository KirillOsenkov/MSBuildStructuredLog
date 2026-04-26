namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Package : NamedNode
    {
        public override string TypeName => nameof(Package);

        public string Version { get; set; }

        public string VersionSpec { get; set; }

        public override string ToString() => TextUtilities.Separate(" ", Name, Version, VersionSpec);

        public override string GetFullText() => TextUtilities.Separate(" ", "Package", Name, Version, VersionSpec);
    }
}