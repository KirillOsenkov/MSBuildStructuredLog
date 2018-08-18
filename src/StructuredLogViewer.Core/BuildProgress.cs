using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer.Core
{
    public class BuildProgress : ObservableObject
    {
        private string progressText;
        public string ProgressText
        {
            get => progressText;

            set
            {
                progressText = value;
                RaisePropertyChanged();
            }
        }

        private string msbuildCommandLine;
        public string MSBuildCommandLine
        {
            get => msbuildCommandLine;

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
