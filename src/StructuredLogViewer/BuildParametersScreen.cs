using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildParametersScreen : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event Action BuildRequested;
        public event Action CancelRequested;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string PrefixArguments { get; set; }
        public string MSBuildArguments { get; set; }
        public string PostfixArguments { get; set; }

        private ICommand buildCommand;
        public ICommand BuildCommand => buildCommand ?? (buildCommand = new Command(Build));
        private void Build() => BuildRequested?.Invoke();

        private ICommand cancelCommand;
        public ICommand CancelCommand => cancelCommand ?? (cancelCommand = new Command(Cancel));
        private void Cancel() => CancelRequested?.Invoke();
    }
}

