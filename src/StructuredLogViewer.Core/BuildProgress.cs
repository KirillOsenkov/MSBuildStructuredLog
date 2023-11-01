namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildProgress : ObservableObject
    {
        public BuildProgress()
        {
        }

        public Progress Progress { get; } = new Progress();

        private string progressText;
        public string ProgressText
        {
            get => progressText;
            set => SetField(ref progressText, value);
        }

        private bool isIndeterminate;
        public bool IsIndeterminate
        {
            get => isIndeterminate;
            set => SetField(ref isIndeterminate, value);
        }

        private double value;
        public double Value
        {
            get => value;
            set => SetField(ref this.value, value);
        }

        private string bufferText;
        public string BufferText
        {
            get => bufferText;
            set => SetField(ref bufferText, value);
        }

        private string msbuildCommandLine;
        public string MSBuildCommandLine
        {
            get => msbuildCommandLine;
            set
            {
                if (SetField(ref msbuildCommandLine, value))
                {
                    RaisePropertyChanged(nameof(ShowCommandLine));
                }
            }
        }

        public bool ShowCommandLine => !string.IsNullOrEmpty(MSBuildCommandLine);
    }
}
