namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Task : TimedNode, IHasSourceFile
    {
        public string FromAssembly { get; set; }
        public string CommandLineArguments { get; set; }
        public string SourceFilePath { get; set; }

        private string title;
        public string Title
        {
            get
            {
                if (title == null)
                {
                    title = Name;
                }

                return title;
            }

            set
            {
                title = value;
            }
        }

        public override string TypeName => nameof(Task);

        public override string ToString() => Title;
    }
}
