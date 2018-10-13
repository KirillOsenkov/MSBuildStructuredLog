namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildProgress : ObservableObject
    {
        private string progressText;
        public string ProgressText
        {
            get => progressText;
            set => SetField(ref progressText, value);
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
