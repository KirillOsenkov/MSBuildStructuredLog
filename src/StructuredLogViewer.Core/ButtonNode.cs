using System;
using System.Windows.Input;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ButtonNode : TextNode
    {
        public ICommand Command => new Command(OnClick);

        public Action OnClick { get; set; }

        private bool isEnabled = true;
        public bool IsEnabled
        {
            get => isEnabled;
            set => SetField(ref isEnabled, value);
        }

        protected override bool IsSelectable => false;

        public override string TypeName => nameof(ButtonNode);
    }
}
