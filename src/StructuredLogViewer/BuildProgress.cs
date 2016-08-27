namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildProgress : ObservableObject
    {
        private string progressText;
        public string ProgressText
        {
            get
            {
                return progressText;
            }

            set
            {
                progressText = value;
                RaisePropertyChanged();
            }
        }

        private string msbuildCommandLine;
        public string MSBuildCommandLine
        {
            get
            {
                return msbuildCommandLine;
            }

            set
            {
                msbuildCommandLine = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ShowCommandLine));
            }
        }

        public bool ShowCommandLine => !string.IsNullOrEmpty(MSBuildCommandLine);
    }
}
