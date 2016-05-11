using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public abstract class BaseNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool isSelected = false;
        public bool IsSelected
        {
            get
            {
                return isSelected;
            }

            set
            {
                if (isSelected == value)
                {
                    return;
                }

                isSelected = value;
                RaisePropertyChanged();
                RaisePropertyChanged("IsLowRelevance");
            }
        }

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
