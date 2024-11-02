namespace Microsoft.Build.Logging.StructuredLogger
{
    public class NameValueNode : BaseNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string NameAndEquals => Name + " = ";
        public string ShortenedValue => TextUtilities.ShortenValue(Value);
        public bool IsValueShortened => Value != ShortenedValue;

        public override string TypeName => nameof(NameValueNode);
        public override string Title => Name;

        public override string ToString() => Name + " = " + Value;
        public override string GetFullText() => ToString();
        public bool IsVisible { get => true; set { } }
        public bool IsExpanded { get => true; set { } }
    }
}
