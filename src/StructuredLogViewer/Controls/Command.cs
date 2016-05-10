using System;
using System.Windows.Input;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Command : ICommand
    {
        private readonly Action execute;

        public Command(Action execute)
        {
            this.execute = execute;
        }

        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => execute();
    }
}
