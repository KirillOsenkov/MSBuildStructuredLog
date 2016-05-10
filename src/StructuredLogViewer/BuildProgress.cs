using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildProgress : INotifyPropertyChanged
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
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
