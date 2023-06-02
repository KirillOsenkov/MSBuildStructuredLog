namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildProgress : ObservableObject
    {
        public BuildProgress()
        {
        }

        public Progress Progress { get; } = new Progress();

        public Progress BufferUsage { get; } = new Progress();

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

        private double bufferValue;
        public double BufferValue
        {
            get => bufferValue;
            set => SetField(ref this.bufferValue, value);
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

        public bool ShowBufferUsage { get; set; } = false;
    }
}
