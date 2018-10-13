using System;
using System.Windows.Input;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class ButtonNode : TextNode
    {
        public ICommand Command => new Command(OnClick);
        public Action OnClick { get; set; }
    }
}
