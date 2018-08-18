using System;
using System.Windows.Input;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class ButtonNode : TextNode
    {
        public ICommand Command => new Command(OnClick);
        public Action OnClick { get; set; }
    }
}
