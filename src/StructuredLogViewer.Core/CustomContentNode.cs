namespace Microsoft.Build.Logging.StructuredLogger
{
    public class CustomContentNode : TextNode
    {
        protected override bool IsSelectable => false;

        public override string TypeName => nameof(CustomContentNode);

        private object content;
        public object Content
        {
            get => content;
            set => SetField(ref content, value);
        }
    }
}
