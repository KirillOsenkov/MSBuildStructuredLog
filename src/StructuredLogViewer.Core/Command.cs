using System.Windows.Input;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Command : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;

        public Command(Action execute)
            : this(execute, () => true)
        {
        }

        public Command(Action execute, Func<bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => canExecute();
        public void Execute(object parameter) => execute();
    }
}
