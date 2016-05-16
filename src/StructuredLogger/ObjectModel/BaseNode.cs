using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public abstract class BaseNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Since there can only be 1 selected node at a time, don't waste an instance field
        /// just to store a bit. Store the currently selected node here and this way we save
        /// 4 bytes per instance (due to layout/alignment). This is huge savings for large 
        /// trees.
        /// </summary>
        private static BaseNode selectedNode = null;

        public bool IsSelected
        {
            get
            {
                return selectedNode == this;
            }

            set
            {
                if (IsSelected == value)
                {
                    RaisePropertyChanged();
                    return;
                }

                selectedNode = value ? this : null;

                RaisePropertyChanged();
                RaisePropertyChanged("IsLowRelevance");
            }
        }

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
